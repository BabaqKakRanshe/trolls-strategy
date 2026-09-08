using UnityEngine;

namespace TrollStrategy.Content
{
    [CreateAssetMenu(fileName = "UnitDefinition", menuName = "TrollStrategy/Content/Unit Definition")]
    public class UnitDefinition : ScriptableObject
    {
        [SerializeField] private UnitKind _kind;
        [SerializeField] private string _displayName = "Unit";
        [SerializeField] private int _price = 40;
        [SerializeField] private int _strength = 3;
        [SerializeField] private float _speed = 5f;
        [SerializeField] private int _cargoCapacity = 10;
        [SerializeField] private string _description = "";
        [SerializeField] private Sprite _portraitSprite;
        [SerializeField] private Sprite[] _idleFrames = new Sprite[0];
        [SerializeField] private Sprite[] _walkFrames = new Sprite[0];
        [SerializeField, Min(0.05f)] private float _spriteScale = 1f;

        public UnitKind Kind => _kind;
        public string DisplayName => _displayName;
        public int Price => _price;
        public int Strength => _strength;
        public float Speed => _speed;
        public int CargoCapacity => _cargoCapacity;
        public string Description => _description;
        public Sprite PortraitSprite => _portraitSprite;
        public Sprite[] IdleFrames => _idleFrames;
        public Sprite[] WalkFrames => _walkFrames;
        public Sprite IdleSprite => _idleFrames != null && _idleFrames.Length > 0 ? _idleFrames[0] : _portraitSprite;
        public Sprite WalkSprite => _walkFrames != null && _walkFrames.Length > 0 ? _walkFrames[0] : IdleSprite;
        public float SpriteScale => _spriteScale > 0f ? _spriteScale : 1f;

        public void Init(UnitKind kind, string displayName, int price, int strength, float speed, int cargoCapacity, string description, Sprite portrait, Sprite[] idleFrames, Sprite[] walkFrames)
        {
            Init(kind, displayName, price, strength, speed, cargoCapacity, description, portrait, idleFrames, walkFrames, SpriteScale);
        }

        public void Init(UnitKind kind, string displayName, int price, int strength, float speed, int cargoCapacity, string description, Sprite portrait, Sprite[] idleFrames, Sprite[] walkFrames, float spriteScale)
        {
            _kind = kind;
            _displayName = displayName;
            _price = price;
            _strength = strength;
            _speed = speed;
            _cargoCapacity = cargoCapacity;
            _description = description;
            _portraitSprite = portrait;
            _idleFrames = idleFrames ?? new Sprite[0];
            _walkFrames = walkFrames ?? new Sprite[0];
            _spriteScale = Mathf.Max(0.05f, spriteScale);
        }

        public void Init(UnitKind kind, string displayName, int price, int strength, float speed, int cargoCapacity, Sprite portrait, Sprite[] idleFrames, Sprite[] walkFrames)
        {
            Init(kind, displayName, price, strength, speed, cargoCapacity, "", portrait, idleFrames, walkFrames);
        }
    }
}
