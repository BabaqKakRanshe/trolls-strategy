using UnityEngine;

namespace TrollStrategy.Content
{
    [CreateAssetMenu(fileName = "UnitDefinition", menuName = "TrollStrategy/Content/Unit Definition")]
    public class UnitDefinition : ScriptableObject
    {
        [Header("Описание")]
        [SerializeField] private UnitKind _kind;
        [SerializeField] private string _displayName = "Unit";
        [SerializeField, TextArea] private string _description = "";

        [Header("Характеристики")]
        [Tooltip("Цена найма в золоте.")]
        [SerializeField, Min(0)] private int _price = 40;
        [Tooltip("Сила: скорость производства. Каждое очко даёт EconomyConfig.WorkPerStrengthSecond работы в секунду.")]
        [SerializeField, Min(0)] private int _strength = 3;
        [Tooltip("Скорость передвижения.")]
        [SerializeField, Min(0f)] private float _speed = 5f;
        [Tooltip("Выносливость в процентах: 100% = 1 единица груза за ходку, 150% = 1.5. Дробная часть копится между ходками.")]
        [SerializeField, Min(1)] private int _stamina = 100;

        [Header("Бой")]
        [SerializeField, Min(1)] private int _combatHealth = 20;
        [SerializeField, Min(1)] private int _combatDamage = 2;
        [SerializeField, Min(0)] private int _combatArmor = 1;
        [SerializeField, Min(100)] private int _attackIntervalMs = 2000;
        [SerializeField, Min(1)] private int _attackRange = 3;

        [Header("Внешний вид")]
        [Tooltip("Портрет для интерфейса.")]
        [SerializeField] private Sprite _portraitSprite;
        [Tooltip("Префаб-вариант UnitBase: спрайт, анимация и размер существа.")]
        [SerializeField] private GameObject _prefab;

        public UnitKind Kind => _kind;
        public string DisplayName => _displayName;
        public int Price => _price;
        public int Strength => _strength;
        public float Speed => _speed;
        public int Stamina => _stamina;
        public int CombatHealth => _combatHealth;
        public int CombatDamage => _combatDamage;
        public int CombatArmor => _combatArmor;
        public int AttackIntervalMs => _attackIntervalMs;
        public int AttackRange => _attackRange;
        public string Description => _description;
        public Sprite PortraitSprite => _portraitSprite;
        public GameObject Prefab => _prefab;

        public void Init(UnitKind kind, string displayName, int price, int strength, float speed, int stamina,
            string description = "", Sprite portrait = null)
        {
            _kind = kind;
            _displayName = displayName;
            _price = price;
            _strength = strength;
            _speed = speed;
            _stamina = Mathf.Max(1, stamina);
            _description = description;
            _portraitSprite = portrait;
        }

        public void SetCombatStats(int health, int damage, int armor, int attackIntervalMs, int attackRange)
        {
            _combatHealth = health;
            _combatDamage = damage;
            _combatArmor = armor;
            _attackIntervalMs = attackIntervalMs;
            _attackRange = attackRange;
        }
    }
}
