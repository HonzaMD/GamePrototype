namespace Assets.Scripts.Ai.Jump
{
    // Sady trajektorii skoku pro druhy prisery. Format viz JumpTrajectory.
    public static class JumpTrajectories
    {
        // Ukazkova sada pro priseru s telem v jedne bunce - k nahrazeni rucne vyladenou sadou.
        public const string SmallStepUp1 = @"
            |  |
            |?#|
            |*?|";

        public const string SmallStepUp2 = @"
            |  |
            | #|
            |??|
            |*?|";

        public const string SmallGap1 = @"
            |   |
            |?  |
            |*x#|";

        public const string SmallGap2 = @"
            |    |
            |    |
            |?   |
            |*xx#|";

        public static readonly JumpDetector Small = new JumpDetector(SmallStepUp1, SmallStepUp2, SmallGap1, SmallGap2);
    }
}
