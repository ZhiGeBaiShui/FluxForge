using FluxForge;
using FluxForge.Story.Hospital;

// 测试所需的游戏数据与 UI 边界替身；不模拟 Unity 协程和渲染，剧情图与交易规则使用生产代码。
public sealed class GameItem
{
    public string identifier;
    public string name;
    public string shortDescription;
    public long unitValue;
    public bool Destroyed;
    public FlowProperties Contents = new FlowProperties();
    public HashSet<string> Tags = new HashSet<string>();
    public bool IsTag(string tag) => Tags.Contains(tag);
    public void Destroy()
    {
        Destroyed = true;
        EmporiumEntry.Instance.backInvinvElement.childItems.Remove(this);
    }
}
public sealed class GameInventory
{
    public List<GameItem> childItems = new List<GameItem>();
}
public sealed class StoreClient
{
    public enum ClientIntent { SELL, BUY, SELLNBUY, DIALOGUE }
    public string identifier;
    public string displayName;
    public string spriteName;
    public string clientFaction;
    public ClientIntent clientIntent;
    public bool dismissable;
    public bool arrestable;
    public Dialogue mainDialogue = new Dialogue();
    public bool noAcceptingContraband;
    public bool IsPersonalConsumption = true;
    public bool CanExposeChem;
    public Func<ItemFeature, StoreClient, bool> canClientExposeFeature;
    public List<string> clientBuyingTagList = new List<string>();
    public int Budget;
    public bool BasicDialogAdded;
    public bool CreationCompleted;
    public Action OnArrival;
    public Action OnDismiss;
    public void SetBudget(int budget) { Budget = budget; }
    public void AddBasicDialog() { BasicDialogAdded = true; }
    public void CompleteClientCreation(bool IgnoreExposeFunc = false)
    {
        CreationCompleted = true;
        if (!IgnoreExposeFunc) canClientExposeFeature = ClientCanExposeFunc.BasicClientExposeCapacity;
        // 模拟原版初始化结束后 Harmony 执行医院派系适配；不加载游戏运行时。
        HospitalClientCreationPatch.Postfix(this, IgnoreExposeFunc);
    }
}
public sealed class ItemFeature { }
public static class ClientCanExposeFunc
{
    public static bool ULClientExposeCapacity(ItemFeature feature, StoreClient client) => client.CanExposeChem;
    public static bool BasicClientExposeCapacity(ItemFeature feature, StoreClient client) => false;
}
public sealed class StoreClientInstance
{
    private readonly StoreClient _blueprint;
    public StoreClientInstance(StoreClient blueprint) { _blueprint = blueprint; }
    public StoreClient GetClientBlueprint() => _blueprint;
}
public static class StoreClientListDict
{
    public static Dictionary<string, Func<StoreClient>> storeClientDict = new Dictionary<string, Func<StoreClient>>();
    private static Dictionary<string, string> displayNameCache = new Dictionary<string, string>();
    public static StoreClient CreateStoreClient(string identifier) => storeClientDict[identifier]();
    public static string GetClientDisplayName(string identifier)
    {
        if (!displayNameCache.TryGetValue(identifier, out string name))
        {
            name = storeClientDict[identifier]().displayName;
            displayNameCache[identifier] = name;
        }
        return name;
    }
    public static void ClearDisplayNameCache() { displayNameCache.Clear(); }
}
public sealed class StoreClientManager
{
    public List<StoreClient> clientStack = new List<StoreClient>();
    public void AddNextClient(StoreClient client) { clientStack.Add(client); }
    public void AddClient(StoreClient client) { clientStack.Add(client); }
}
public static class ModHelper
{
    public static string CurrentLocale = "zh";
    public static string GetLocalized(string modId, string key, params object[] args)
    {
        Dictionary<string, string> table = FluxForge.Story.Tests.NativeLocalizationData.Read(CurrentLocale);
        string value;
        if (!table.TryGetValue(key, out value))
        {
            value = FluxForge.Story.Tests.NativeLocalizationData.Read("en").TryGetValue(key, out string english)
                ? english : key;
        }
        return args.Length == 0 ? value : string.Format(value, args);
    }
    public static void SetClientSource(StoreClient client, string source) { }
}
public static class ModHook
{
    public static event Action OnGameLoadedLate;
    public static event Action OnShutterOpenedLate;
    public static event Action<StoreClientManager> OnGenerateCustomerLate;
    public static void FireGameLoaded() { OnGameLoadedLate?.Invoke(); }
    public static void FireShutterOpened() { OnShutterOpenedLate?.Invoke(); }
    public static void FireGenerateCustomer(StoreClientManager manager) { OnGenerateCustomerLate?.Invoke(manager); }
}
public sealed class EmporiumEntry
{
    public static EmporiumEntry Instance = new EmporiumEntry();
    public GameInventory backInvinvElement = new GameInventory();
    public GameInventory frontInvinvElement = new GameInventory();
}
public sealed class PlayerStore
{
    private static PlayerStore _instance;
    public static PlayerStore Instance
    {
        get { return _instance ??= new PlayerStore(); }
        set { _instance = value; }
    }
    public static bool HasInstance => _instance != null;
    public StoreClientManager storeClientManager = new StoreClientManager();
    public StoreClientInstance currentClientInstance;
    public int Dismissals;
    public void DismissCurrentClient(bool callNextWhenPossible)
    {
        currentClientInstance.GetClientBlueprint().OnDismiss?.Invoke();
        currentClientInstance = null;
        Dismissals++;
    }
    public string playerName = "店主";
    public Dictionary<string, string> modData = new Dictionary<string, string>();
    public int Cash;
    public List<StoreReputation> storeReputations = new List<StoreReputation>();
    public void ModCash(int amount) { Cash += amount; }
    public void AddDirectSellingItemToTable(GameItem item, bool owned)
    {
        EmporiumEntry.Instance.frontInvinvElement.childItems.Add(item);
    }
}
public static class StoreStation
{
    public static int Day;
    public static int GetDayCounter() => Day;
}
public sealed class ReputationUIManager
{
    public static ReputationUIManager Instance = new ReputationUIManager();
    public List<string> Notifications = new List<string>();
    public void Notify(string text) { Notifications.Add(text); }
    public void NotifyReputation(string factionId, int value) { }
}
public sealed class Dialogue
{
    public string Speaker;
    public string Text;
    public Action endAction;
    public float endActionDelay;
    public Action StartAction;
    public bool IsEndActionExecuted;
    public Dialogue Next;
    public List<(string Text, Dialogue Next)> Choices = new List<(string, Dialogue)>();
    public void ExecuteEndAction()
    {
        if (!IsEndActionExecuted && endAction != null)
        {
            IsEndActionExecuted = true;
            endAction();
        }
    }
    public void SetText(string speaker, string text) { Speaker = speaker; Text = text; }
    public Dialogue NextDialogue() { Next = new Dialogue(); return Next; }
    public void SetNextDialogue(Dialogue next) { Next = next; }
    public void SetStartAction(Action action) { StartAction = action; }
    public void AddChoice(GameItem icon, string text, Dialogue choiceNextDialogue)
    {
        Choices.Add((text, choiceNextDialogue));
    }
}
public sealed class DialogUIManager
{
    public static DialogUIManager Instance = new DialogUIManager();
    public Dialogue Current;
    public bool isSkipCurrentDialog;
    public int Starts;
    public void StartDialogue(Dialogue dialogue, bool forcedStart) { Starts++; Current = dialogue; }
    public void InterruptDialog() { Current?.ExecuteEndAction(); Current = null; }
    public Dialogue GetCurrentDialog() => Current;
    public void SetCurrentDialog(Dialogue dialogue) { Current = dialogue; }
    public void CurrentClientLeave() { PlayerStore.Instance.DismissCurrentClient(false); }
}
public static class DirectoryMaster
{
    public static GameItem Item(string key) => new GameItem { identifier = key };
}
public static class PreBuiltItemHelper
{
    public static GameItem CreateChemicalBottle(string type) => new GameItem();
    public static GameItem CreateHandnote(string text, string title, string author, int style) => new GameItem();
}
public static class GeneralHelper
{
    public static void SetItemOwned(GameItem item, bool owned) { }
}
public static class GraphUtils
{
    public static void EmporiumTryAdd(GameInventory inventory, GameItem item) { inventory.childItems.Add(item); }
}
public static class RNG
{
    public static int PickOffset;
    public static int RollSample;
    public static int LastChance;
    public static bool Roll(int chance) { LastChance = chance; return RollSample < chance; }
    public static int GetRandomInt(int min, int max) => min + PickOffset;
    public static float GetRandomFloat(float min, float max) => min;
}
namespace FluxForge.Mod
{
    internal static class FlowTags
    {
        internal const string LiquidContainerTag = "liquid";
        internal const string FlowDevice = "device";
    }
    internal static class FlowStock
    {
        internal static int Reads;
        internal static FlowProperties Read(GameItem item) { Reads++; return item.Contents; }
        internal static void Write(GameItem item, FlowProperties properties) { item.Contents = properties; }
    }
}
// 原版声望/本地化边界；HospitalReputation及其注册文本使用真实生产代码。
public sealed class StoreReputationPerk
{
    public string perkId;
    public int perkThreshold;
    public int perkType;
    public string iconName;
    public bool isUnlocked;
}
public sealed class StoreReputation
{
    public string factionId;
    public string factionDisplay;
    public string factionColor1;
    public string factionColor2;
    public string factionColor3;
    public double Amount;
    public List<StoreReputationPerk> perks = new List<StoreReputationPerk>();
    public int GetReputation() => (int)Amount;
    public void ModReputation(double amount) { Amount += amount; UpdateReputation(); }
    public static StoreReputation GetStoreReputation(string id)
        => PlayerStore.Instance.storeReputations.FirstOrDefault(r => r.factionId == id);
    public static bool IsPerkUnlocked(string id)
        => PlayerStore.Instance.storeReputations.SelectMany(r => r.perks).Any(p => p.perkId == id && p.isUnlocked);
    public static void UpdateReputation()
    {
        foreach (var reputation in PlayerStore.Instance.storeReputations)
        foreach (var perk in reputation.perks)
        {
            perk.isUnlocked = perk.perkThreshold < 0
                ? reputation.Amount <= perk.perkThreshold
                : reputation.Amount >= perk.perkThreshold;
        }
    }
}
internal static class TestHospitalReputation
{
    internal static int Value
    {
        get { return HospitalReputation.Get(); }
        set
        {
            StoreReputation.GetStoreReputation(HospitalReputation.FactionId).Amount = value;
            StoreReputation.UpdateReputation();
        }
    }
}
namespace UnityEngine.Localization.Tables
{
    public sealed class StringTable
    {
        public Dictionary<string, string> Entries = new Dictionary<string, string>();
        public void AddEntry(string key, string value) { Entries[key] = value; }
    }
}
namespace UnityEngine.Localization.Settings
{
    public static class LocalizationSettings
    {
        public static readonly TestStringDatabase StringDatabase = new TestStringDatabase();
    }
    public sealed class TestStringDatabase
    {
        public readonly UnityEngine.Localization.Tables.StringTable ReputationTable
            = new UnityEngine.Localization.Tables.StringTable();
        public UnityEngine.Localization.Tables.StringTable GetTable(string name) => ReputationTable;
    }
}
namespace FluxForge.Story.Portraits
{
    internal static class NpcPortraitRegistry
    {
        internal const string AcidBuyerSpriteKey = "vera";
    }
}
namespace HarmonyLib
{
    public sealed class HarmonyPatch : Attribute
    {
        public HarmonyPatch(Type type, string method) { }
        public HarmonyPatch(Type type, string method, Type[] arguments) { }
    }
}

public sealed class EscapeUIManager
{
    public void OnMainMenu() { }
}
public sealed class GameMaster
{
    public void QuitToMenu() { }
}
public sealed class SecData { }
