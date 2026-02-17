# Professional Architecture Analysis: Inventory & Crafting System

현재 Lemegeton Project의 시스템은 **기능 구현 관점에서는 완성도가 높고 직관적**입니다. 하지만 프로젝트 규모가 커지거나, 멀티플레이어/라이브 서비스 환경으로 확장될 경우를 대비한 **실무 레벨의 개선 포인트(Design Patterns & Optimization)**를 제안합니다.

---

## 1. 아키텍처 개선 (Architecture & Patterns)

### 📊 Model-View-Presenter (MVP) 패턴 도입

**현재**: `InventoryManager`가 데이터 관리와 로직을 모두 수행하며, UI는 이벤트(`OnInventoryChanged`)를 단순 구독합니다.
**개선**:

- **Model**: `InventoryRepository` (순수 데이터 저장소)
- **Presenter**: `InventoryPresenter` (UI 로직, 필터링, 정렬 담당)
- **View**: `InventoryView` (순수 UI 표시)
- **이점**: UI가 변경되어도 로직(Model)을 건드릴 필요가 없으며, 유닛 테스트가 용이해집니다.

### 🔌 인터페이스 분리 (Iterface Segregation - SOLID)

**현재**: `InventoryManager` 클래스 하나에 의존.
**개선**: `IInventory` 인터페이스 정의.

```csharp
public interface IInventory {
    bool AddItem(string id, int count);
    bool ConsumeItem(string id, int count);
    int GetItemCount(string id);
}
```

- **이점**: 플레이어 인벤토리뿐만 아니라 **창고(Storage), 상점(Shop), 몬스터 드랍** 등도 동일한 `IInventory` 인터페이스로 처리할 수 있어 확장성이 비약적으로 상승합니다.

### ⚡ Command 패턴 (Transaction & Undo)

**현재**: `AddItem`, `ConsumeItem`이 즉시 실행되고 되돌릴 수 없음.
**개선**: `IInventoryCommand`를 통해 행위를 캡슐화.

- **이점**: "제작 실패 시 재료 롤백", "아이템 실수로 버림 - 실행 취소(Undo)" 기능을 구현하기 쉬워집니다. 특히 트랜잭션(Transaction) 관리가 필요한 복잡한 제작 시스템에서 필수적입니다.

---

## 2. 데이터 구조 최적화 (Data Structure Optimization)

### 🚀 Dictionary Lookup (O(1) 검색)

**현재**: `GetItemCount` 호출 시 `InventoryItem[]` 전체를 Loop 돕니다(O(N)). 슬롯이 100개, 200개로 늘어나면 성능 저하가 발생할 수 있습니다 (매 프레임/UI 갱신 시).
**개선**: 내부적으로 `Dictionary<string, int> cachedCounts`를 유지하여 아이템 개수를 즉시 반환.

```csharp
// 캐싱 예시
private Dictionary<string, int> _itemCountCache;
public void AddItem(...) {
   // ... 로직 수행 ...
   _itemCountCache[id] += amount; // 캐시 갱신
}
```

### 📦 Flyweight 패턴 (Data Memory)

**현재**: `InventoryItem` 클래스(`class`)를 사용하여 GC(가비지 컬렉터) 부하가 발생할 수 있습니다.
**개선**:

- `ItemData` (ScriptableObject)는 이미 Flyweight 패턴의 일종입니다(잘 되어 있음).
- `InventoryItem`을 `struct`로 변경하거나, 미리 할당된 **Object Pool**을 사용하여 GC Spike를 방지할 수 있습니다.

---

## 3. 반응형 프로그래밍 (Reactive Extensions)

### 📡 R3 (UniRx) 도입

**현재**: `OnInventoryChanged` 이벤트 하나로 **전체 UI**를 다시 그립니다 ("Dirty Flag" 방식). 아이템 하나만 바뀌어도 모든 슬롯을 갱신하는 비효율이 있습니다.
**개선**: **ReactiveProperty**를 사용하여 **변경된 슬롯만** 콕 집어서 갱신.

```csharp
// 예시 (R3 / UniRx)
public ReactiveCollection<InventoryItem> Slots = new();
// UI 측
slots[i].ObserveEveryValueChanged(x => x.count).Subscribe(UpdateCountText);
```

- **이점**: UI 갱신 비용 최소화, 코드의 간결성 증가.

---

## 3. 금회 적용 사항 및 기대 효과 (Applied Changes & Benefits)

이번 리팩토링에서는 **기존 구조를 유지(Non-Breaking)**하면서 실무 수준의 최적화를 적용하는 것을 목표로 합니다.

### A. IInventory 인터페이스 도입 (Interface Segregation)

- **변경 이유 (Why)**: 현재는 `InventoryManager` 클래스에 직접 의존하고 있어, 나중에 '창고'나 '상점'을 만들 때 똑같은 기능(아이템 넣기/빼기)을 또 만들어야 합니다.
- **기대 효과 (Benefit)**:
  - **확장성**: `IInventory` 하나만 있으면 플레이어 가방, 창고, 몬스터 전리품 주머니 등 무엇이든 동일하게 취급할 수 있습니다.
  - **결합도 감소**: 다른 시스템(제작, 상점)이 `InventoryManager`라는 구체적인 클래스가 아니라 `IInventory`라는 약속(Interface)하고만 대화하므로 구조가 유연해집니다.

### B. Dictionary Caching (O(1) Lookup)

- **변경 이유 (Why)**: 현재 `GetItemCount` 함수는 호출될 때마다 인벤토리 전체 슬롯을 처음부터 끝까지 루프(Loop)를 돕니다. 슬롯이 100개, 1000개로 늘어나거나 매 프레임 UI를 갱신하면 게임이 끊길 수 있습니다.
- **기대 효과 (Benefit)**:
  - **성능 최적화**: 아이템 개수를 별도의 Dictionary(`_itemCache`)에 실시간으로 기억해둡니다. 이제 아이템이 100만 개가 있어도, 개수를 세는 데 걸리는 시간은 **0초(즉시)**가 됩니다. (Time Complexity: O(N) -> O(1))
  - **반응성**: 빈번한 UI 갱신에도 성능 저하가 없습니다.

---

## 4. 추천 로드맵 (Roadmap for Pro-Level)

1.  **Phase 1 (Interface)**: `IInventory`, `ICraftingTable` 인터페이스 추출하여 결합도 낮추기.
2.  **Phase 2 (Optimization)**: `Dictionary` 캐싱 도입으로 검색 속도 최적화.
3.  **Phase 3 (View Logic)**: UI 코드(`CampCraftPage`)에서 비즈니스 로직(재료 검사 등)을 분리하여 `CraftingSystem` 클래스로 이동.

이러한 개선은 프로젝트가 **대규모 RPG**로 발전하거나 **팀 프로젝트**로 전환될 때 빛을 발하는 구조입니다.
