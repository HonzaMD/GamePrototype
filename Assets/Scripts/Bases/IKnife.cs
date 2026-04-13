namespace Assets.Scripts.Bases
{
    public interface IKnife
    {
        bool IsActive { get; }
        float GetDmg();
        float GetJointCutStretchLimit();
    }
}
