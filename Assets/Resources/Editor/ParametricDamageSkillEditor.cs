using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ParametricDamageSkill))]
[CanEditMultipleObjects]
public class ParametricDamageSkillEditor : Editor
{
    // === SkillAsset(부모) 공통 필드 ===
    SerializedProperty displayNameProp;
    SerializedProperty descriptionImageProp;
    SerializedProperty descriptionProp;
    SerializedProperty CostProp;
    SerializedProperty cooldownTurnsProp;
    SerializedProperty useGapCloseJumpProp;
    SerializedProperty legacyIdProp;
    SerializedProperty trainingRoutesProp;
    SerializedProperty animKindProp;
    SerializedProperty animTriggerOverrideProp;
    SerializedProperty targetAlignmentProp;

    // ParametricDamage 고유 + 래핑 필드
    SerializedProperty priorityMode;
    SerializedProperty preferredStatus;
    SerializedProperty areaPreset;
    SerializedProperty useProvidedUnitTarget;
    SerializedProperty powerOverride;
    SerializedProperty damageSchool;
    SerializedProperty conditionalMultipliers;
    SerializedProperty selectionMode;
    SerializedProperty diagUseNEAxis;
    SerializedProperty applyStatusOnHitProp;
    SerializedProperty changeTileToProp;

    // 상태 소비 프로퍼티
    SerializedProperty consumeStateOnCast;
    SerializedProperty statesToConsume;

    // 투사체 관련 필드
    SerializedProperty projectilePrefab;
    SerializedProperty projectileSpeed;

    // Training 관련
    SerializedProperty trainingUseAreaOverride;
    SerializedProperty routeForAreaOverride;
    SerializedProperty trainingAreaPreset;
    SerializedProperty trainingDiagUseNEAxis;

    SerializedProperty routeForSuppression;
    SerializedProperty trainingSuppressionOnHit;

    SerializedProperty trainingApplyBleed;
    SerializedProperty routeForBleed;
    SerializedProperty trainingBleedStacks;
    SerializedProperty trainingBleedDurationTurns;

    SerializedProperty trainingApplyDefenseStacks;
    SerializedProperty routeForDefenseStacks;
    SerializedProperty trainingDefenseStatusId;
    SerializedProperty trainingDefenseStacks;
    SerializedProperty trainingDefenseDurationTurns;

    SerializedProperty trainingUseKnockback;
    SerializedProperty routeForKnockback;

    SerializedProperty trainingUsePostMove;
    SerializedProperty routeForPostMove;
    SerializedProperty trainingPostMoveRange;

    SerializedProperty trainingHitAllEnemies;
    SerializedProperty routeForHitAllEnemies;

    SerializedProperty trainingUseMultiHit;
    SerializedProperty trainingHitCount;

    SerializedProperty trainingUseSelfAtkBuff;
    SerializedProperty routeForSelfAtkBuff;
    SerializedProperty selfAtkBuffId;
    SerializedProperty selfAtkBuffDurationTurns;

    SerializedProperty trainingApplyAgiDebuff;
    SerializedProperty routeForAgiDebuff;
    SerializedProperty targetAgiDebuffId;
    SerializedProperty targetAgiDebuffDurationTurns;

    SerializedProperty trainingApplyFear;
    SerializedProperty routeForFear;
    SerializedProperty fearDurationTurns;

    SerializedProperty trainingRefundOnKill;
    SerializedProperty routeForRefundOnKill;

    SerializedProperty trainingReduceHostility;
    SerializedProperty routeForReduceHostility;
    SerializedProperty trainingHostilityMultiplier;

    SerializedProperty useFrontlineBonus;
    SerializedProperty frontlineDepth;
    SerializedProperty frontlineMultiplier;
    SerializedProperty useManualFrontier;
    SerializedProperty manualFrontierPlayer;
    SerializedProperty manualFrontierEnemy;
    SerializedProperty manualSecondLayerPlayer;
    SerializedProperty manualSecondLayerEnemy;
    SerializedProperty playerFrontlineDir;
    SerializedProperty enemyFrontlineDir;

    void OnEnable()
    {
        // === 부모(SkillAsset) 쪽 필드 ===
        displayNameProp = serializedObject.FindProperty("displayName");
        descriptionImageProp = serializedObject.FindProperty("descriptionImage");
        descriptionProp = serializedObject.FindProperty("description");
        CostProp = serializedObject.FindProperty("mpCost");
        cooldownTurnsProp = serializedObject.FindProperty("cooldownTurns");
        useGapCloseJumpProp = serializedObject.FindProperty("useGapCloseJump");
        legacyIdProp = serializedObject.FindProperty("legacyId");
        trainingRoutesProp = serializedObject.FindProperty("trainingRoutes");
        animKindProp = serializedObject.FindProperty("animKind");
        animTriggerOverrideProp = serializedObject.FindProperty("animTriggerOverride");
        priorityMode = serializedObject.FindProperty("priorityMode");
        targetAlignmentProp = serializedObject.FindProperty("targetAlignment");

        // === ParametricDamageSkill 고유 필드 ===
        priorityMode = serializedObject.FindProperty("priorityMode");
        preferredStatus = serializedObject.FindProperty("preferredStatus");
        areaPreset = serializedObject.FindProperty("areaPreset");
        useProvidedUnitTarget = serializedObject.FindProperty("useProvidedUnitTarget");
        powerOverride = serializedObject.FindProperty("powerOverride");
        damageSchool = serializedObject.FindProperty("damageSchool");
        conditionalMultipliers = serializedObject.FindProperty("conditionalMultipliers");
        selectionMode = serializedObject.FindProperty("selectionMode");
        diagUseNEAxis = serializedObject.FindProperty("diagUseNEAxis");
        applyStatusOnHitProp = serializedObject.FindProperty("applyStatusOnHit");
        changeTileToProp = serializedObject.FindProperty("changeTileTo");

        // 상태 소비 프로퍼티 연결
        consumeStateOnCast = serializedObject.FindProperty("consumeStateOnCast");
        statesToConsume = serializedObject.FindProperty("statesToConsume");

        // 투사체 프로퍼티 연결
        projectilePrefab = serializedObject.FindProperty("projectilePrefab");
        projectileSpeed = serializedObject.FindProperty("projectileSpeed");

        // Training
        trainingUseAreaOverride = serializedObject.FindProperty("trainingUseAreaOverride");
        routeForAreaOverride = serializedObject.FindProperty("routeForAreaOverride");
        trainingAreaPreset = serializedObject.FindProperty("trainingAreaPreset");
        trainingDiagUseNEAxis = serializedObject.FindProperty("trainingDiagUseNEAxis");

        routeForSuppression = serializedObject.FindProperty("routeForSuppression");
        trainingSuppressionOnHit = serializedObject.FindProperty("trainingSuppressionOnHit");

        trainingApplyBleed = serializedObject.FindProperty("trainingApplyBleed");
        routeForBleed = serializedObject.FindProperty("routeForBleed");
        trainingBleedStacks = serializedObject.FindProperty("trainingBleedStacks");
        trainingBleedDurationTurns = serializedObject.FindProperty("trainingBleedDurationTurns");

        trainingApplyDefenseStacks = serializedObject.FindProperty("trainingApplyDefenseStacks");
        routeForDefenseStacks = serializedObject.FindProperty("routeForDefenseStacks");
        trainingDefenseStatusId = serializedObject.FindProperty("trainingDefenseStatusId");
        trainingDefenseStacks = serializedObject.FindProperty("trainingDefenseStacks");
        trainingDefenseDurationTurns = serializedObject.FindProperty("trainingDefenseDurationTurns");

        trainingUseKnockback = serializedObject.FindProperty("trainingUseKnockback");
        routeForKnockback = serializedObject.FindProperty("routeForKnockback");

        trainingUsePostMove = serializedObject.FindProperty("trainingUsePostMove");
        routeForPostMove = serializedObject.FindProperty("routeForPostMove");
        trainingPostMoveRange = serializedObject.FindProperty("trainingPostMoveRange");

        trainingHitAllEnemies = serializedObject.FindProperty("trainingHitAllEnemies");
        routeForHitAllEnemies = serializedObject.FindProperty("routeForHitAllEnemies");

        trainingUseMultiHit = serializedObject.FindProperty("trainingUseMultiHit");
        trainingHitCount = serializedObject.FindProperty("trainingHitCount");

        trainingUseSelfAtkBuff = serializedObject.FindProperty("trainingUseSelfAtkBuff");
        routeForSelfAtkBuff = serializedObject.FindProperty("routeForSelfAtkBuff");
        selfAtkBuffId = serializedObject.FindProperty("selfAtkBuffId");
        selfAtkBuffDurationTurns = serializedObject.FindProperty("selfAtkBuffDurationTurns");

        trainingApplyAgiDebuff = serializedObject.FindProperty("trainingApplyAgiDebuff");
        routeForAgiDebuff = serializedObject.FindProperty("routeForAgiDebuff");
        targetAgiDebuffId = serializedObject.FindProperty("targetAgiDebuffId");
        targetAgiDebuffDurationTurns = serializedObject.FindProperty("targetAgiDebuffDurationTurns");

        trainingApplyFear = serializedObject.FindProperty("trainingApplyFear");
        routeForFear = serializedObject.FindProperty("routeForFear");
        fearDurationTurns = serializedObject.FindProperty("fearDurationTurns");

        trainingRefundOnKill = serializedObject.FindProperty("trainingRefundOnKill");
        routeForRefundOnKill = serializedObject.FindProperty("routeForRefundOnKill");

        trainingReduceHostility = serializedObject.FindProperty("trainingReduceHostility");
        routeForReduceHostility = serializedObject.FindProperty("routeForReduceHostility");
        trainingHostilityMultiplier = serializedObject.FindProperty("trainingHostilityMultiplier");

        useFrontlineBonus = serializedObject.FindProperty("useFrontlineBonus");
        frontlineDepth = serializedObject.FindProperty("frontlineDepth");
        frontlineMultiplier = serializedObject.FindProperty("frontlineMultiplier");
        useManualFrontier = serializedObject.FindProperty("useManualFrontier");
        manualFrontierPlayer = serializedObject.FindProperty("manualFrontierPlayer");
        manualFrontierEnemy = serializedObject.FindProperty("manualFrontierEnemy");
        manualSecondLayerPlayer = serializedObject.FindProperty("manualSecondLayerPlayer");
        manualSecondLayerEnemy = serializedObject.FindProperty("manualSecondLayerEnemy");
        playerFrontlineDir = serializedObject.FindProperty("playerFrontlineDir");
        enemyFrontlineDir = serializedObject.FindProperty("enemyFrontlineDir");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // === Base Skill (SkillAsset 공통) ===
        EditorGUILayout.LabelField("Base Skill", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(displayNameProp);
        EditorGUILayout.PropertyField(descriptionImageProp);
        EditorGUILayout.PropertyField(descriptionProp);
        EditorGUILayout.PropertyField(CostProp, new GUIContent("MP Cost"));
        EditorGUILayout.PropertyField(cooldownTurnsProp, new GUIContent("Cooldown Turns"));
        EditorGUILayout.PropertyField(useGapCloseJumpProp, new GUIContent("Use Gap Close Jump"));
        EditorGUILayout.PropertyField(legacyIdProp, new GUIContent("Legacy Id"));

        // Targeting Rules 표시
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("Targeting Rules (아군/적군 구분)", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(targetAlignmentProp);
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space();

        // === Animation 설정 표시 ===
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Animation", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(animKindProp, new GUIContent("Anim Kind"));
        EditorGUILayout.PropertyField(animTriggerOverrideProp, new GUIContent("Anim Trigger Override"));

        // 투사체 설정
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Projectile Settings (Ranged Only)", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(projectilePrefab, new GUIContent("Projectile Prefab"));
        EditorGUILayout.PropertyField(projectileSpeed, new GUIContent("Projectile Speed"));

        EditorGUILayout.Space();
        EditorGUILayout.PropertyField(trainingRoutesProp, true);

        // 상태 소비 섹션
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("State Consumption", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(consumeStateOnCast, new GUIContent("Consume State?"));
        if (consumeStateOnCast.boolValue)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(statesToConsume, new GUIContent("State to Remove"));
            EditorGUI.indentLevel--;
        }

        // === ParametricDamage 고유 설정 ===
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Targeting", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(priorityMode);
        EditorGUILayout.PropertyField(preferredStatus);
        EditorGUILayout.PropertyField(areaPreset);
        EditorGUILayout.PropertyField(useProvidedUnitTarget);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Damage", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(powerOverride);
        EditorGUILayout.PropertyField(damageSchool);
        EditorGUILayout.PropertyField(conditionalMultipliers, true);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Mode", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(selectionMode);
        EditorGUILayout.PropertyField(diagUseNEAxis);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Training Effects", EditorStyles.boldLabel);

        // === 범위 변경 ===
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("범위 변경", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(trainingUseAreaOverride, new GUIContent("훈련 사용"));
        if (trainingUseAreaOverride.boolValue)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(routeForAreaOverride);
            EditorGUILayout.PropertyField(trainingAreaPreset);
            EditorGUILayout.PropertyField(trainingDiagUseNEAxis);
            EditorGUI.indentLevel--;
        }
        EditorGUILayout.EndVertical();

        // === 제압 부여 ===
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("제압 부여 설정", EditorStyles.boldLabel);
        EditorGUI.indentLevel++;
        EditorGUILayout.PropertyField(routeForSuppression);
        EditorGUILayout.PropertyField(trainingSuppressionOnHit);
        EditorGUI.indentLevel--;
        EditorGUILayout.EndVertical();

        // === 출혈 부여 ===
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("출혈 부여 설정", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(trainingApplyBleed, new GUIContent("훈련 사용"));
        if (trainingApplyBleed.boolValue)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(routeForBleed);
            EditorGUILayout.PropertyField(trainingBleedStacks);
            EditorGUILayout.PropertyField(trainingBleedDurationTurns);
            EditorGUI.indentLevel--;
        }
        EditorGUILayout.EndVertical();

        // === 방어 중첩 버프 ===
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("방어 중첩 버프", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(trainingApplyDefenseStacks, new GUIContent("훈련 사용"));
        if (trainingApplyDefenseStacks.boolValue)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(routeForDefenseStacks);
            EditorGUILayout.PropertyField(trainingDefenseStatusId);
            EditorGUILayout.PropertyField(trainingDefenseStacks);
            EditorGUILayout.PropertyField(trainingDefenseDurationTurns);
            EditorGUI.indentLevel--;
        }
        EditorGUILayout.EndVertical();

        // === 넉백 ===
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("넉백 설정", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(trainingUseKnockback, new GUIContent("훈련 사용"));
        if (trainingUseKnockback.boolValue)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(routeForKnockback);
            EditorGUI.indentLevel--;
        }
        EditorGUILayout.EndVertical();

        // === 추가 이동 ===
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("추가 이동 설정", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(trainingUsePostMove, new GUIContent("훈련 사용"));
        if (trainingUsePostMove.boolValue)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(routeForPostMove);
            EditorGUILayout.PropertyField(trainingPostMoveRange);
            EditorGUI.indentLevel--;
        }
        EditorGUILayout.EndVertical();

        // === 전체 공격 ===
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("전체 공격 설정", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(trainingHitAllEnemies, new GUIContent("훈련 사용"));
        if (trainingHitAllEnemies.boolValue)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(routeForHitAllEnemies);
            EditorGUI.indentLevel--;
        }
        EditorGUILayout.EndVertical();

        // === 멀티 히트 ===
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("멀티 히트 설정", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(trainingUseMultiHit, new GUIContent("훈련 사용"));
        if (trainingUseMultiHit.boolValue)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(trainingHitCount);
            EditorGUI.indentLevel--;
        }
        EditorGUILayout.EndVertical();

        // === 자기 물리 대미지 버프 ===
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("물리 대미지 버프 설정", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(trainingUseSelfAtkBuff, new GUIContent("훈련 사용"));
        if (trainingUseSelfAtkBuff.boolValue)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(routeForSelfAtkBuff);
            EditorGUILayout.PropertyField(selfAtkBuffId);
            EditorGUILayout.PropertyField(selfAtkBuffDurationTurns);
            EditorGUI.indentLevel--;
        }
        EditorGUILayout.EndVertical();

        // === 타겟 민첩 약화 ===
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("타겟 민첩 약화 적용 설정", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(trainingApplyAgiDebuff, new GUIContent("훈련 사용"));
        if (trainingApplyAgiDebuff.boolValue)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(routeForAgiDebuff);
            EditorGUILayout.PropertyField(targetAgiDebuffId);
            EditorGUILayout.PropertyField(targetAgiDebuffDurationTurns);
            EditorGUI.indentLevel--;
        }
        EditorGUILayout.EndVertical();

        // === 공포 상태 부여 ===
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("공포 상태 부여 설정", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(trainingApplyFear, new GUIContent("훈련 사용"));
        if (trainingApplyFear.boolValue)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(routeForFear);
            EditorGUILayout.PropertyField(fearDurationTurns, new GUIContent("지속 턴 수"));
            EditorGUI.indentLevel--;
        }
        EditorGUILayout.EndVertical();

        // === 자원 반환 ===
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("자원 반환 설정", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(trainingRefundOnKill, new GUIContent("훈련 사용"));
        if (trainingRefundOnKill.boolValue)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(routeForRefundOnKill);
            EditorGUI.indentLevel--;
        }
        EditorGUILayout.EndVertical();

        // === 적의 감소 ===
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("적의(Hostility) 훈련", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(trainingReduceHostility, new GUIContent("적의 생성 감소 사용"));
        if (trainingReduceHostility.boolValue)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(routeForReduceHostility, new GUIContent("활성 루트"));
            EditorGUILayout.PropertyField(trainingHostilityMultiplier, new GUIContent("적의 생성 배율(0.5=반감)"));
            EditorGUI.indentLevel--;
        }
        EditorGUILayout.EndVertical();

        // === Frontline Bonus ===
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Frontline Bonus", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(useFrontlineBonus, new GUIContent("사용"));
        if (useFrontlineBonus.boolValue)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(frontlineDepth);
            EditorGUILayout.PropertyField(frontlineMultiplier);
            EditorGUILayout.PropertyField(useManualFrontier);
            EditorGUILayout.PropertyField(manualFrontierPlayer, true);
            EditorGUILayout.PropertyField(manualFrontierEnemy, true);
            EditorGUILayout.PropertyField(manualSecondLayerPlayer, true);
            EditorGUILayout.PropertyField(manualSecondLayerEnemy, true);
            EditorGUILayout.PropertyField(playerFrontlineDir);
            EditorGUILayout.PropertyField(enemyFrontlineDir);
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space();
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("Hit Effects & Tile (확장)", EditorStyles.boldLabel);

        EditorGUILayout.PropertyField(applyStatusOnHitProp, new GUIContent("Hit Status Effects"), true);
        EditorGUILayout.PropertyField(changeTileToProp, new GUIContent("Change Tile To"));

        EditorGUILayout.EndVertical();

        serializedObject.ApplyModifiedProperties();
    }
}