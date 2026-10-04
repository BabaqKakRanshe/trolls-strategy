using System.Collections.Generic;
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
        [Tooltip("Имена, которые получают нанятые существа этого вида.")]
        [SerializeField] private List<string> _names = new();
        [Tooltip("Прозвища: добавляются к имени, когда все имена уже заняты.")]
        [SerializeField] private List<string> _epithets = new();

        [Header("Характеристики")]
        [Tooltip("Цена найма в золоте.")]
        [SerializeField, Min(0)] private int _price = 40;
        [Tooltip("Сила: скорость производства. Каждое очко даёт EconomyConfig.WorkPerStrengthSecond работы в секунду.")]
        [SerializeField, Min(0)] private int _strength = 3;
        [Tooltip("Скорость передвижения.")]
        [SerializeField, Min(0f)] private float _speed = 5f;
        [Tooltip("Выносливость в процентах: 100% = 1 единица груза за ходку, 150% = 1.5. Дробная часть копится между ходками.")]
        [SerializeField, Min(1)] private int _stamina = 100;
        [Tooltip("Сколько протоптанности добавляет каждый шаг в новую клетку: тяжёлые существа протаптывают тропы быстрее.")]
        [SerializeField, Range(0, 10)] private int _trailWear = 1;

        [Tooltip("Здания, где существо работает лучше других.")]
        [SerializeField] private BuildingKind[] _favoredBuildings = System.Array.Empty<BuildingKind>();
        [Tooltip("На сколько процентов больше работы оно даёт в любимых зданиях.")]
        [SerializeField, Min(0)] private int _favoredWorkPercent;
        [Tooltip("Можно ли нанять это существо в колонию (иначе оно встречается только на арене).")]
        [SerializeField] private bool _hireable = true;

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
        public int TrailWear => _trailWear;
        public int CombatHealth => _combatHealth;
        public int CombatDamage => _combatDamage;
        public int CombatArmor => _combatArmor;
        public int AttackIntervalMs => _attackIntervalMs;
        public int AttackRange => _attackRange;
        public string Description => _description;
        public System.Collections.Generic.IReadOnlyList<BuildingKind> FavoredBuildings =>
            _favoredBuildings ?? System.Array.Empty<BuildingKind>();
        public int FavoredWorkPercent => _favoredWorkPercent;
        public bool Hireable => _hireable;

        public bool Favors(BuildingKind kind) => System.Array.IndexOf(_favoredBuildings ?? System.Array.Empty<BuildingKind>(), kind) >= 0;

        public void SetWorkTraits(bool hireable, int favoredWorkPercent, params BuildingKind[] favoredBuildings)
        {
            _hireable = hireable;
            _favoredWorkPercent = Mathf.Max(0, favoredWorkPercent);
            _favoredBuildings = favoredBuildings ?? System.Array.Empty<BuildingKind>();
        }

        public void SetPrefab(GameObject prefab) => _prefab = prefab;
        public void SetPortrait(Sprite portrait) => _portraitSprite = portrait;
        public void SetTrailWear(int wear) => _trailWear = Mathf.Clamp(wear, 0, 10);

        public IReadOnlyList<string> Names => _names;
        public IReadOnlyList<string> Epithets => _epithets;
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

        public void SetNames(IEnumerable<string> names, IEnumerable<string> epithets = null)
        {
            _names = new List<string>(names);
            _epithets = epithets != null ? new List<string>(epithets) : new List<string>();
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
