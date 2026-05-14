using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using Unity.Services.Economy.Model;
using UnityEngine;
using UnityEngine.UI; // 이미지 처리를 위해 필수

public class InventoryRowUI : MonoBehaviour
{
    [Header("UI Components")]
    [SerializeField] private Image iconImage;          // ★ 새로 추가됨: 아이콘 표시할 이미지
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI instanceText;
    [SerializeField] private TextMeshProUGUI optionText;
    [SerializeField] private Button sellBtn;

    // 내부 변수
    private string playersInventoryItemId;
    private Func<int> getPrice;
    private Func<string, int, Task> createListingAsync;

    // ★ Bind 함수에 Sprite 파라미터 추가됨
    public void Bind(
        PlayersInventoryItem item,
        Func<int> getSellPrice,
        Func<string, int, Task> createListingFunc,
        Sprite iconSprite)
    {
        playersInventoryItemId = item.PlayersInventoryItemId;
        getPrice = getSellPrice;
        createListingAsync = createListingFunc;

        // 1. 아이콘 적용
        if (iconImage != null)
        {
            iconImage.sprite = iconSprite;
            // 이미지가 찌그러지지 않게 비율 유지 (선택사항)
            iconImage.preserveAspect = true;
        }

        // 2. 텍스트 적용
        if (titleText != null) titleText.text = item.InventoryItemId;

        string shortInstance = !string.IsNullOrEmpty(playersInventoryItemId) && playersInventoryItemId.Length > 8
            ? playersInventoryItemId.Substring(0, 8) + "..."
            : playersInventoryItemId ?? "(null)";

        if (instanceText != null) instanceText.text = $"ID: {shortInstance}";

        // 옵션 데이터(InstanceData)가 있으면 여기서 파싱 (지금은 비워둠)
        if (optionText != null) optionText.text = "Option: -";

        // 3. 버튼 연결
        if (sellBtn != null)
        {
            sellBtn.onClick.RemoveAllListeners();
            sellBtn.onClick.AddListener(() => { _ = SellAsync(); });
        }
    }

    private async Task SellAsync()
    {
        if (string.IsNullOrEmpty(playersInventoryItemId)) return;
        if (createListingAsync == null) return;

        int price = getPrice != null ? getPrice.Invoke() : 100;

        // 판매 로직 실행
        await createListingAsync.Invoke(playersInventoryItemId, price);
    }
}