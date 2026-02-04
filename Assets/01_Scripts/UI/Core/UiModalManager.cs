using System.Collections.Generic;
using UnityEngine;

public interface IModalWindow
{
    bool IsOpen { get; }
    void Show();
    void Hide();
    GameObject Root { get; } // 배치/정렬용
    int Priority { get; }    // 우선순위(원하면 사용)
}

public class UiModalManager : MonoBehaviour
{
    public static UiModalManager Instance { get; private set; }

    // (옵션) 스택 기능을 켜면 새 창 띄울 때 기존은 남기고 가림 → 닫으면 직전 창 복귀
    [SerializeField] bool useStack = false;

    readonly Stack<IModalWindow> stack = new();
    IModalWindow current; // useStack=false일 때만 사용

    void Awake()
    {
        if (Instance && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void Toggle(IModalWindow w)
    {
        if (w == null) return;
        if (w.IsOpen) Close(w);
        else Open(w);
    }

    public void Open(IModalWindow w)
    {
        if (w == null) return;

        if (useStack)
        {
            // 이미 같은 창이면 무시
            if (stack.Count > 0 && ReferenceEquals(stack.Peek(), w)) return;
            // 최상단만 보이게: 기존 최상단이 열려있다면 가려두기(필요시 Hide 호출)
            if (stack.Count > 0 && stack.Peek().IsOpen) stack.Peek().Hide();
            stack.Push(w);
            w.Show();
        }
        else
        {
            // 하나만 허용: 열려있는 창 전부 닫기
            if (current != null && current.IsOpen) current.Hide();
            current = w;
            w.Show();
        }
    }

    public void Close(IModalWindow w)
    {
        if (w == null) return;

        if (useStack)
        {
            if (stack.Count == 0) return;
            // 최상단만 닫을 수 있게(안전)
            if (!ReferenceEquals(stack.Peek(), w))
            {
                // 최상단이 아니면 모두 닫고 초기화(단순화)
                foreach (var x in stack) { if (x.IsOpen) x.Hide(); }
                stack.Clear();
                return;
            }
            w.Hide();
            stack.Pop();
            // 이전 창 복귀
            if (stack.Count > 0) stack.Peek().Show();
        }
        else
        {
            if (ReferenceEquals(current, w))
            {
                w.Hide();
                current = null;
            }
            else
            {
                // 다른 창이 닫히려는 경우도 안전하게 처리
                if (w.IsOpen) w.Hide();
            }
        }
    }

    // 편의 메서드
    public void CloseAll()
    {
        if (useStack)
        {
            while (stack.Count > 0) { var x = stack.Pop(); if (x.IsOpen) x.Hide(); }
        }
        else
        {
            if (current != null && current.IsOpen) current.Hide();
            current = null;
        }
    }

    // ESC 규칙: 뭔가 열려 있으면 그 창만 닫기, 아무 것도 없으면 options 열기
    public void OnEscape(IModalWindow optionsPreferred)
    {
        if (useStack)
        {
            if (stack.Count > 0)
            {
                Close(stack.Peek());
                return;
            }
        }
        else
        {
            if (current != null)
            {
                Close(current);
                return;
            }
        }
        if (optionsPreferred != null) Open(optionsPreferred);
    }
}
