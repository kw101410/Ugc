using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using Unity.Services.Authentication;
using Unity.Services.CloudCode;
using Unity.Services.Economy;
using Unity.Services.Economy.Model;
using UnityEngine;
using UnityEngine.UI; // 이미지 처리를 위해 필수

[System.Serializable]
public struct ItemIconMapping
{
    public string id;      // 예: JUNGCOIN
    public Sprite icon;    // 인스펙터에서 연결할 이미지
}

public class PortfolioMarketDemo : MonoBehaviour
{
    [Header("Economy IDs")]
    [SerializeField] private string currencyId = "COIN";

    [Header("Random Give Pool (Resource IDs)")]
    // 니가 바꾼 ID들로 기본값 세팅함
    [SerializeField] private string[] randomGiveItemIds = { "RED_DIAMOND", "DIAMOND", "GOLD" };

    [Header("Icon Settings")]
    public Sprite defaultIcon;             // 아이콘 없을 때 띄울 기본 이미지 (물음표 등)
    public List<ItemIconMapping> iconList; // ID와 이미지를 연결하는 리스트

    [Header("Top UI")]
    [SerializeField] private TextMeshProUGUI debugLine;
    [SerializeField] private TextMeshProUGUI coinText;
    [SerializeField] private TMP_InputField priceInput;
    [SerializeField] private Button refreshBtn;
    [SerializeField] private Button giveEquipmentBtn;
    [SerializeField] private Button addCoinBtn;
    [SerializeField] private Button claimBtn;

    [Header("Inventory UI")]
    [SerializeField] private Transform inventoryContent;
    [SerializeField] private InventoryRowUI inventoryRowPrefab;

    [Header("Market UI")]
    [SerializeField] private Transform marketContent;
    [SerializeField] private MarketRowUI marketRowPrefab;
    [SerializeField] private Button marketRefreshBtn;

    [Header("Market Options")]
    [SerializeField] private int marketLimit = 30;
    [SerializeField] private string marketSort = "NEWEST"; // NEWEST / PRICE_ASC / PRICE_DESC

    private bool isEconomyConfigSynced = false;

    private void Start()
    {
        if (refreshBtn != null) refreshBtn.onClick.AddListener(() => _ = RefreshAllAsync());
        if (giveEquipmentBtn != null) giveEquipmentBtn.onClick.AddListener(() => _ = GiveRandomItemAsync());
        if (addCoinBtn != null) addCoinBtn.onClick.AddListener(() => _ = AddCoinAsync(100));
        if (claimBtn != null) claimBtn.onClick.AddListener(() => _ = ClaimEarningsAsync());
        if (marketRefreshBtn != null) marketRefreshBtn.onClick.AddListener(() => _ = RefreshMarketAsync());
    }

    // ID로 아이콘 스프라이트 찾아주는 함수
    private Sprite GetIconById(string id)
    {
        if (iconList == null) return defaultIcon;

        foreach (var map in iconList)
        {
            if (map.id == id) return map.icon;
        }
        return defaultIcon;
    }

    public async Task RefreshAllAsync()
    {
        if (!AuthenticationService.Instance.IsSignedIn)
        {
            SetMessage("로그인 필요");
            return;
        }

        await EnsureEconomyConfigSyncedAsync();

        await RefreshCoinsAsync();
        await RefreshInventoryAsync();
        await RefreshMarketAsync();
    }

    private async Task EnsureEconomyConfigSyncedAsync()
    {
        if (isEconomyConfigSynced) return;

        try
        {
            await EconomyService.Instance.Configuration.SyncConfigurationAsync();
            isEconomyConfigSynced = true;
            Debug.Log("[Economy] Configuration synced");
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            SetMessage("Economy Sync 실패 (Publish/환경/프로젝트 확인)");
        }
    }

    private void SetMessage(string message)
    {
        if (debugLine != null) debugLine.text = message;
        Debug.Log(message);
    }

    private int GetSellPrice()
    {
        if (priceInput == null) return 100;
        if (int.TryParse(priceInput.text, out int price)) return Mathf.Max(1, price);
        return 100;
    }

    // -------------------------
    // Economy: Coin
    // -------------------------
    private async Task RefreshCoinsAsync()
    {
        try
        {
            var balances = await EconomyService.Instance.PlayerBalances.GetBalancesAsync();
            long coin = 0;

            foreach (var b in balances.Balances)
            {
                if (b.CurrencyId == currencyId)
                {
                    coin = b.Balance;
                    break;
                }
            }

            if (coinText != null) coinText.text = coin.ToString();
        }
        catch (EconomyException e)
        {
            Debug.LogException(e);
            SetMessage("코인 조회 실패 (Economy Publish/환경 확인)");
        }
    }

    private async Task AddCoinAsync(long amount)
    {
        try
        {
            await EnsureEconomyConfigSyncedAsync();

            int delta = ToSafeInt(amount);
            await EconomyService.Instance.PlayerBalances.IncrementBalanceAsync(currencyId, delta);
            await RefreshCoinsAsync();
            SetMessage($"코인 +{amount}");
        }
        catch (EconomyException e)
        {
            Debug.LogException(e);
            SetMessage("코인 증가 실패 (Economy Publish/통화 ID 확인)");
        }
    }

    private int ToSafeInt(long value)
    {
        if (value > int.MaxValue) return int.MaxValue;
        if (value < int.MinValue) return int.MinValue;
        return (int)value;
    }

    // -------------------------
    // Economy: Inventory
    // -------------------------
    private async Task RefreshInventoryAsync()
    {
        try
        {
            ClearChildren(inventoryContent);

            GetInventoryResult inv = await EconomyService.Instance.PlayerInventory.GetInventoryAsync();
            List<PlayersInventoryItem> items = inv.PlayersInventoryItems;

            foreach (var item in items)
            {
                if (inventoryRowPrefab == null || inventoryContent == null) continue;

                InventoryRowUI row = Instantiate(inventoryRowPrefab, inventoryContent);

                // ★ 여기서 이미지(Icon)도 같이 넘겨줌!
                row.Bind(item, GetSellPrice, CreateListingAsync, GetIconById(item.InventoryItemId));
            }

            SetMessage($"인벤 로드 완료: {items.Count}개");
        }
        catch (EconomyException e)
        {
            Debug.LogException(e);
            SetMessage("인벤 조회 실패 (Economy Publish/로그인 상태 확인)");
        }
    }

    // -------------------------
    // Give Random
    // -------------------------
    private async Task GiveRandomItemAsync()
    {
        if (!AuthenticationService.Instance.IsSignedIn)
        {
            SetMessage("로그인 먼저");
            return;
        }

        await EnsureEconomyConfigSyncedAsync();

        if (randomGiveItemIds == null || randomGiveItemIds.Length == 0)
        {
            SetMessage("randomGiveItemIds 비어있음");
            return;
        }

        string templateId = PickRandomId(randomGiveItemIds);
        Debug.Log($"[GiveRandom] templateId='{templateId}'");

        try
        {
            PlayersInventoryItem created =
                await EconomyService.Instance.PlayerInventory.AddInventoryItemAsync(templateId);

            SetMessage($"지급 성공: {created.InventoryItemId}");
            await RefreshInventoryAsync();
        }
        catch (EconomyException e)
        {
            Debug.LogException(e);
            SetMessage($"지급 실패: '{templateId}' (Resource ID 확인 필요)");
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            SetMessage("지급 실패: 기타 예외");
        }
    }

    private string PickRandomId(string[] ids)
    {
        int index = UnityEngine.Random.Range(0, ids.Length);
        return (ids[index] ?? "").Trim();
    }

    // -------------------------
    // Cloud Code: Marketplace
    // -------------------------
    private async Task CreateListingAsync(string playersInventoryItemId, int price)
    {
        try
        {
            var args = new Dictionary<string, object>
            {
                { "players_inventory_item_id", playersInventoryItemId },
                { "price", price },
                { "currency_id", currencyId }
            };

            CreateListingResult res = await CloudCodeService.Instance.CallEndpointAsync<CreateListingResult>(
                "Mkt_CreateListing",
                args
            );

            SetMessage($"등록 완료: {res.listingId}");
            await RefreshAllAsync();
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            SetMessage("등록 실패 (Cloud Code 확인)");
        }
    }

    private async Task RefreshMarketAsync()
    {
        try
        {
            ClearChildren(marketContent);

            var args = new Dictionary<string, object>
            {
                { "limit", marketLimit },
                { "sort", marketSort }
            };

            MarketListResult res = await CloudCodeService.Instance.CallEndpointAsync<MarketListResult>(
                "Mkt_GetActiveListings",
                args
            );

            if (res.listings == null)
            {
                SetMessage("거래소 목록 0개");
                return;
            }

            foreach (var listing in res.listings)
            {
                if (marketRowPrefab == null || marketContent == null) continue;

                MarketRowUI row = Instantiate(marketRowPrefab, marketContent);

                // ★ 여기서도 이미지(Icon) 넘겨줌!
                row.Bind(listing, BuyListingAsync, CancelListingAsync, GetIconById(listing.inventoryItemId));
            }

            SetMessage($"거래소 로드: {res.listings.Length}개");
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            SetMessage("거래소 조회 실패");
        }
    }

    private async Task BuyListingAsync(string listingId)
    {
        try
        {
            var args = new Dictionary<string, object> { { "listing_id", listingId } };
            BuyResult res = await CloudCodeService.Instance.CallEndpointAsync<BuyResult>("Mkt_BuyListing", args);
            SetMessage($"구매 완료: {res.newPlayersInventoryItemId}");
            await RefreshAllAsync();
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            SetMessage("구매 실패");
        }
    }

    private async Task CancelListingAsync(string listingId)
    {
        try
        {
            var args = new Dictionary<string, object> { { "listing_id", listingId } };
            CancelResult res = await CloudCodeService.Instance.CallEndpointAsync<CancelResult>("Mkt_CancelListing", args);
            SetMessage("취소 완료");
            await RefreshAllAsync();
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            SetMessage("취소 실패");
        }
    }

    private async Task ClaimEarningsAsync()
    {
        try
        {
            var args = new Dictionary<string, object> { { "currency_id", currencyId } };
            ClaimResult res = await CloudCodeService.Instance.CallEndpointAsync<ClaimResult>("Mkt_ClaimEarnings", args);
            SetMessage($"정산 수령: {res.claimed}");
            await RefreshAllAsync();
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            SetMessage("정산 실패");
        }
    }

    private static void ClearChildren(Transform parent)
    {
        if (parent == null) return;
        foreach (Transform child in parent) Destroy(child.gameObject);
    }

    // -------------------------
    // DTOs
    // -------------------------
    [Serializable]
    public class CreateListingResult { public string listingId; }

    [Serializable]
    public class MarketListResult { public ListingDto[] listings; }

    [Serializable]
    public class ListingDto
    {
        public string listingId;
        public string status;
        public string sellerPlayerId;
        public string inventoryItemId;
        public Dictionary<string, object> instanceData;
        public string currencyId;
        public int price;
        public long createdAt;
    }

    [Serializable]
    public class BuyResult { public bool ok; public string newPlayersInventoryItemId; }

    [Serializable]
    public class CancelResult { public bool ok; public string returnedPlayersInventoryItemId; }

    [Serializable]
    public class ClaimResult { public bool ok; public int claimed; }
}