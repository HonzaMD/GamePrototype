namespace Assets.Scripts.Ai
{
    public enum ActionStatus
    {
        Running,   // pravidlo drzi pohybovy zamek dal
        Done,      // pravidlo skoncilo, zamek se uvolnuje
    }

    // Pravidlo souperici o POHYBOVY ZAMEK. V jednu chvili drzi zamek jen jedno pravidlo,
    // poradi v MonsterSettings.Rules je priorita (shora = nejvyssi).
    // Tri druhy chovani pokryva jedno rozhrani:
    //   instant     - Begin udela efekt, Tick vrati hned Done (zamek nedrzi)
    //   one-shot    - drzi zamek, Tick pocita animaci, typicky CanBeInterrupted = false
    //   spojite     - Tick si sam pretestuje podminku a vrati Done, az pomine
    // Implementace MUSI byt bezstavova - vsechny instance druhu sdili jeden MonsterSettings asset.
    // Durativni stav patri do scratche na MonsterBrain.
    public interface IAiRule
    {
        bool Test(MonsterBrain brain);           // vstupni podminka; bezstavova, no-alloc
        bool CanBeInterrupted { get; }           // smi ho prebit VYSSI pravidlo
        void Begin(MonsterBrain brain);          // jednou pri ziskani zamku (init scratche, efekt)
        ActionStatus Tick(MonsterBrain brain);   // kazdy fixed krok, dokud drzi zamek
    }

    // REFLEX. Bezi kazdy fixed krok, i kdyz nejake pravidlo drzi zamek, a zamek nikdy nebere
    // ani nezastavi kaskadu pravidel. Meni kontext/flagy, na ktere pak navazuji pravidla a styl
    // (retezeni je ukol modifieru, pravidla se nikdy neretezi).
    // Voditko: modifier at neprepisuje primarni pohybovy prikaz, jen kontext.
    public interface IAiModifier
    {
        bool Test(MonsterBrain brain);
        void Apply(MonsterBrain brain);
    }
}
