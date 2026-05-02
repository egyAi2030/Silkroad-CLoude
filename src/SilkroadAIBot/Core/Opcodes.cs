namespace SilkroadAIBot.Networking
{
    public static class Opcode
    {
        // Identity & Handshake
        public const ushort GLOBAL_IDENTIFICATION = 0x2001;
        public const ushort HANDSHAKE = 0x5000;
        public const ushort HANDSHAKE_ACCEPT = 0x9000;
        public const ushort CLIENT_KEEPALIVE = 0x2002;
        public const ushort SERVER_GUARD_CHALLENGE_1 = 0x2005;
        public const ushort SERVER_GUARD_CHALLENGE_2 = 0x6005;

        // Login (vSRO / Private Server Flow)
        public const ushort CLIENT_PATCH_CHECK = 0x6100;
        public const ushort SERVER_PATCH_RESPONSE = 0xA100;
        public const ushort CLIENT_SERVER_LIST_REQUEST = 0x6101;
        public const ushort SERVER_SERVER_LIST_RESPONSE = 0xA101;
        public const ushort CLIENT_LOGIN_REQUEST = 0x6102;
        public const ushort SERVER_LOGIN_REDIRECT = 0xA102;
        public const ushort SERVER_LOGIN_ERROR = 0xA103;
        public const ushort SERVER_VPLUS_AUTH_ERROR = 0xAA30;
        public const ushort SERVER_VPLUS_UI_MENU = 0xAA33;
        public const ushort SERVER_VPLUS_VERSION_INFO = 0xAA4E;
        public const ushort SERVER_VPLUS_SEC_SYNC = 0xAA01;
        public const ushort CLIENT_VPLUS_AUTH_TOKEN = 0xF000;
        public const ushort CLIENT_VPLUS_ACTION = 0xF04B; // vSRO Plus custom action
        public const ushort CLIENT_VPLUS_DATA_REQ = 0xF027; // vSRO Plus data request
        
        public const ushort SERVER_CAPTCHA_CHALLENGE = 0x2322;
        public const ushort CLIENT_CAPTCHA_RESPONSE = 0x6103;
        public const ushort CLIENT_AGENT_AUTH = 0x6103; 
        public const ushort CLIENT_HWID_SECURITY = 0x9001;

        // Character Selection
        public const ushort CLIENT_CHARACTER_SELECTION_JOIN_REQUEST = 0x7001;
        public const ushort SERVER_CHARACTER_SELECTION_JOIN_RESPONSE = 0xB001;
        public const ushort CLIENT_CHARACTER_SELECTION_ACTION_REQUEST = 0x7007;
        public const ushort SERVER_CHARACTER_SELECTION_ACTION_RESPONSE = 0xB007;

        // Game Data Loading
        public const ushort SERVER_CHARACTER_DATA_BEGIN = 0x34A5;
        public const ushort SERVER_CHARACTER_DATA = 0x3013;
        public const ushort SERVER_CHARACTER_DATA_END = 0x34A6;
        public const ushort CLIENT_CHARACTER_CONFIRM_SPAWN = 0x3012;
        public const ushort SERVER_PLAYER_STATS = 0x303D;
        public const ushort SERVER_CHAT_MESSAGE = 0x3026;

        // Entity Spawn/Despawn
        public const ushort SERVER_ENTITY_SPAWN = 0x3015;
        public const ushort SERVER_SINGLE_SPAWN = 0x3019;
        public const ushort SERVER_ENTITY_DESPAWN = 0x3016;
        public const ushort SERVER_SINGLE_DESPAWN = 0x3016;
        public const ushort SERVER_GROUP_DESPAWN = 0x3017;
        public const ushort SERVER_ENTITY_UPDATE_STATUS = 0x3057;
        public const ushort SERVER_ENTITY_MOVEMENT = 0x30D2;
        public const ushort SERVER_ENTITY_HPMP_UPDATE = 0x3054;
        public const ushort SERVER_ENTITY_HPMP_AUTO = 0x3056;
        public const ushort SERVER_ENTITY_LEVEL_UP = 0x3056;
        public const ushort SERVER_ENTITY_DIE = 0x30BF;
        public const ushort SERVER_XP_UPDATE = 0x305C;

        // Movement & Action
        public const ushort CLIENT_CHARACTER_MOVEMENT = 0x7021;
        public const ushort CLIENT_CHARACTER_ACTION_REQUEST = 0x7074;
        public const ushort CLIENT_CHARACTER_ACTION_REQUEST_SELECTION = 0x7045;
        public const ushort SERVER_CHARACTER_SELECTION_RESPONSE = 0xB045;
        public const ushort SERVER_CHARACTER_ACTION_RESPONSE = 0xB074;
        public const ushort SERVER_BUFF_INFO = 0xB0BD;
        public const ushort CLIENT_CHARACTER_PICKUP = 0x704B;

        // Inventory
        public const ushort CLIENT_INVENTORY_ITEM_USE = 0x704C;
        public const ushort SERVER_INVENTORY_ITEM_USE = 0xB04C;
        public const ushort CLIENT_INVENTORY_ITEM_MOVEMENT = 0x7034;
        public const ushort SERVER_INVENTORY_ITEM_MOVEMENT = 0xB034;

        // Party & Match
        public const ushort CLIENT_PARTY_CREATE = 0x7060;
        public const ushort SERVER_PARTY_CREATE_RESPONSE = 0xB060;
        public const ushort CLIENT_PARTY_MATCHING_FORM = 0x7069;
        public const ushort CLIENT_PARTY_MATCHING_CHANGE = 0x706A;
        public const ushort CLIENT_PARTY_MATCHING_DELETE = 0x706B;
        public const ushort CLIENT_PARTY_MATCHING_LIST = 0x706C;
        public const ushort CLIENT_PARTY_MATCHING_JOIN = 0x706D;

        // Alchemy
        public const ushort CLIENT_ALCHEMY_REINFORCE = 0x7150;
        public const ushort CLIENT_ALCHEMY_ENCHANT = 0x7151;
        public const ushort CLIENT_ALCHEMY_MANUFACTURE = 0x7155;
        public const ushort CLIENT_ALCHEMY_DISMANTLE = 0x7157;
        
        // Stall
        public const ushort CLIENT_STALL_CREATE = 0x70B1;
        public const ushort CLIENT_STALL_DESTROY = 0x70B2;
        public const ushort CLIENT_STALL_TALK = 0x70B3;
        public const ushort CLIENT_STALL_BUY = 0x70B4;
        public const ushort CLIENT_STALL_LEAVE = 0x70B5;
        public const ushort CLIENT_STALL_UPDATE = 0x70BA;
        
        // vSRO Plus UI Opcodes
        public const ushort SERVER_VPLUS_MASTERY_UPDATE = 0xAA17;
        public const ushort SERVER_VPLUS_SKILL_UPDATE = 0xAA18;
        public const ushort SERVER_VPLUS_STAT_UPDATE = 0xAA1A;
    }
}
