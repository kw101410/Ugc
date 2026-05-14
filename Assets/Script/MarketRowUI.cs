using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI; // 이미지 처리를 위해 필수

public class MarketRowUI : MonoBehaviour
{
    [Header("UI Components")]
    [SerializeField] private Image iconImage;          // ★ 새로 추가됨: 아이콘 표시할 이미지
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI priceText;
    [SerializeField] private Button buyBtn;
    [SerializeField] private Button cancelBtn;

    // 내부 변수
    private string listingId;
    private Func<string, System.Threading.Tasks.Task> buyAsync;
    private Func<string, System.Threading.Tasks.Task> cancelAsync;

    // ★ Bind 함수에 Sprite 파라미터 추가됨
    public void Bind(
        PortfolioMarketDemo.ListingDto listing,
        Func<string, System.Threading.Tasks.Task> buyFunc,
        Func<string, System.Threading.Tasks.Task> cancelFunc,
        Sprite iconSprite)
    {
        listingId = listing.listingId;
        buyAsync = buyFunc;
        cancelAsync = cancelFunc;

        // 1. 아이콘 적용
        if (iconImage != null)
        {
            iconImage.sprite = iconSprite;
            iconImage.preserveAspect = true;
        }

        // 2. 텍스트 적용
        if (titleText != null) titleText.text = listing.inventoryItemId;
        if (priceText != null) priceText.text = $"{listing.price} {listing.currencyId}";

        // 3. 구매 버튼 연결
        if (buyBtn != null)
        {
            buyBtn.onClick.RemoveAllListeners();
            buyBtn.onClick.AddListener(() => _ = BuyAsync());
        }

        // 4. 취소 버튼 연결
        if (cancelBtn != null)
        {
            cancelBtn.onClick.RemoveAllListeners();
            cancelBtn.onClick.AddListener(() => _ = CancelAsync());
        }
    }

    private async System.Threading.Tasks.Task BuyAsync()
    {
        if (buyAsync == null) return;
        await buyAsync.Invoke(listingId);
    }

    private async System.Threading.Tasks.Task CancelAsync()
    {
        if (cancelAsync == null) return;
        await cancelAsync.Invoke(listingId);
    }
}