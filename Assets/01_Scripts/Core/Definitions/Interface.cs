public interface IResettable
{
    void ResetState();
}
public interface ITrainableSkill
{
    public struct TrainingOption
    {
        public string title;
        public string description;
    }

    // UI에 3가지 루트의 제목/설명을 제공 (길이 3 고정 권장)
    TrainingOption[] GetTrainingOptions();
}