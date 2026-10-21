namespace Assets.Scripts.Ai.Jump
{
    // Sady trajektorii skoku pro druhy prisery. Format viz JumpTrajectory.
    public static class JumpTrajectories
    {
        private static readonly string[] SmallTrajectories = new string[] {
          @"|  |
            | #|
            | ?|
            |??|
            |*?|
",@"
            |   |
            |  #|
            |  ?|
            |?x?|
            |*x?|
",@"
            |?   |
            |   #|
            |   ?|
            |?xx?|
            |*xx?|
",@"
            |  |
            | #|
            |??|
            |*?|
",@"
            |   |
            |  #|
            |?x?|
            |*x?|
",@"
            |?   |
            |   #|
            |?xx?|
            |*xx?|
",@"
            |?    |
            |    #|
            |?xxx?|
            |*xxx?|
",@"
            |?     |
            |     #|
            |? xxx?|
            |*xxxx?|
",@"
            |  |
            |?#|
            |*?|
",@"
            |   |
            |? #|
            |*x?|
",@"
            |?   |
            |    |
            |? x#|
            |*xx?|
",@"
            |?    |
            |     |
            |? xx#|
            |*xxx?|
",@"
            |?     |
            |      |
            |? xxx#|
            |*xxxx?|
",@"
            |??     |
            |       |
            |?  xxx#|
            |*xxxxx?|
",@"
            |??     ?|
            |        |
            |?  xxxx#|
            |*xxxxxx?|
",@"
            |   |
            |?x |
            |*x#|
",@"
            |    |
            |?xx |
            |*xx#|
",@"
            |     |
            |?    |
            |*xxx#|
",@"
            |?    ?|
            |      |
            |?     |
            |*xxxx#|
",@"
            |?     ?|
            |       |
            |?  x   |
            |*xxxxx#|
",@"
            |??    ??|
            |        |
            |?  xx   |
            |*xxxxxx#|
",@"
            |??     ??|
            |         |
            |?  xxx   |
            |*xxxxxxx#|
",@"
            |??      ??|
            |          |
            |?  xxxx   |
            |*xxxxxxxx#|
",@"
            |? |
            |* |
            |?#|
",@"
            |?  |
            |*  |
            |?x#|
",@"
            |?   |
            |*   |
            |?xx#|
",@"
            |    ?|
            |?    |
            |*xx  |
            |?xxx#|
",@"
            |     ?|
            |?     |
            |*xxx  |
            |?xxxx#|
",@"
            |     ??|
            |?      |
            |*xxxx  |
            |?xxxxx#|
",@"
            |      ??|
            |?       |
            |*xxxxx  |
            |?xxxxxx#|
",@"
            |? |
            |* |
            |? |
            |?#|
",@"
            |?  |
            |*  |
            |?x |
            |?x#|
",@"
            |?  ?|
            |*   |
            |?x  |
            |?xx#|
",@"
            |?   ?|
            |*    |
            |?xx  |
            |?xxx#|
",@"
            |?    ?|
            |?    ?|
            |*xx   |
            |?xxx  |
            |?xxxx#|
",@"
            |? |
            |* |
            |? |
            |? |
            |?#|
",@"
            |?  |
            |*  |
            |?x |
            |?x |
            |?x#|
",@"
            |?  ?|
            |*   |
            |?x  |
            |?xx |
            |?xx#|

",@"
            |? |
            |*#|
"
        };

        private static JumpDetector small;

        // Detektor se stavi line pri prvnim pozadavku.
        public static JumpDetector Get(JumpSet set) => set switch
        {
            JumpSet.Small => small ??= new JumpDetector(SmallTrajectories),
            _ => throw new System.ArgumentOutOfRangeException(nameof(set), set, null),
        };
    }

    // Vyber sady trajektorii na prefabu (sady jsou v kodu).
    public enum JumpSet
    {
        Small,
    }
}
