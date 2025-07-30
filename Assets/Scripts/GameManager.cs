using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    private Deck deck;
    private List<Card> playerHand = new List<Card>();
    private List<Card> aiHand = new List<Card>();
    private List<Card> table = new List<Card>();
    private bool isPlayerTurn = true;
    private bool isPlayerAttacking;
    private string trumpSuit;
    private bool isProcessingAction = false;

    [SerializeField] private Transform playerHandPanel;
    [SerializeField] private Transform aiHandPanel;
    [SerializeField] private Transform tablePanel;
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private GameObject cardPrefab;
    [SerializeField] private Sprite debugSprite; // Added for fallback
    [SerializeField] private Button passButton;  // Readded
    [SerializeField] private Button takeButton;  // Readded

    private float actionDelay = 3f;

    private PlayerControls controls;

    private string GetSpriteRank(string rank)
    {
        switch (rank)
        {
            case "Jack": return "J";
            case "Queen": return "Q";
            case "King": return "K";
            case "Ace": return "A";
            default: return rank;
        }
    }

    private string GetSpriteSuit(string suit)
    {
        switch (suit)
        {
            case "Spades": return "S";
            case "Clubs": return "C";
            case "Diamonds": return "D";
            case "Hearts": return "H";
            default: return suit;
        }
    }

    private Sprite LoadCardSprite(string spriteName)
    {
        Sprite sprite = Resources.Load<Sprite>(spriteName);
        if (sprite != null) return sprite;
        Debug.LogWarning($"Sprite not found: {spriteName} (using debugSprite)");
        return debugSprite; // Fallback to debugSprite
    }

    void Awake()
    {
        controls = new PlayerControls();
        controls.Player.Click.performed += ctx => OnCardClick();
        controls.Enable();
    }

    void OnDestroy() => controls.Disable();

    void Start()
    {
        deck = GetComponent<Deck>();
        if (deck == null)
        {
            Debug.LogWarning("Deck component not found. Adding and initializing Deck.");
            deck = gameObject.AddComponent<Deck>();
            deck.Awake(); // Ensure initialization
        }
        trumpSuit = deck.GetTrumpSuit() ?? "Spades"; // Default to Spades if null
        DealInitialCards();
        isPlayerAttacking = true;
        isPlayerTurn = true;
        SetupUI();
        UpdateStatus();
        Debug.Log($"Initial state: isPlayerTurn={isPlayerTurn}, isPlayerAttacking={isPlayerAttacking}, Deck={deck.CardsRemaining()}, Trump={trumpSuit}");
    }

    void DealInitialCards()
    {
        if (deck == null || deck.CardsRemaining() == 0)
        {
            Debug.LogError("Deck is null or empty. Reinitializing.");
            if (deck == null) deck = gameObject.AddComponent<Deck>();
            deck.Awake(); // Force reinitialization
        }
        for (int i = 0; i < 6; i++)
        {
            var playerCard = deck.DrawCard();
            var aiCard = deck.DrawCard();
            if (playerCard != null) playerHand.Add(playerCard);
            if (aiCard != null) aiHand.Add(aiCard);
            if (playerCard == null || aiCard == null)
            {
                Debug.LogError($"Deck ran out of cards at index {i}. Remaining: {deck.CardsRemaining()}");
                break;
            }
        }
        UpdateHandUI();
    }

    void SetupUI()
    {
        passButton.onClick.AddListener(PlayerPass);
        takeButton.onClick.AddListener(PlayerTake);
        UpdateHandUI();
    }

    void UpdateHandUI()
    {
        ClearPanel(playerHandPanel);
        ClearPanel(aiHandPanel);
        ClearPanel(tablePanel);

        foreach (var card in playerHand)
        {
            var cardObj = Instantiate(cardPrefab, playerHandPanel);
            card.GameObject = cardObj;
            var text = cardObj.GetComponentInChildren<TextMeshProUGUI>();
            text.text = card.ToString();
            text.enabled = false;
            string spriteName = $"{GetSpriteRank(card.Rank)}{GetSpriteSuit(card.Suit)}";
            Image image = cardObj.GetComponent<Image>();
            image.sprite = LoadCardSprite(spriteName);
            var button = cardObj.GetComponent<Button>();
            button.interactable = isPlayerTurn && !isProcessingAction;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => PlayerPlayCard(card));
            cardObj.AddComponent<BoxCollider2D>().size = image.rectTransform.sizeDelta; // Ensure clickable
        }

        foreach (var card in aiHand)
        {
            var cardObj = Instantiate(cardPrefab, aiHandPanel);
            var text = cardObj.GetComponentInChildren<TextMeshProUGUI>();
            text.text = "Card";
            text.enabled = false;
            Image image = cardObj.GetComponent<Image>();
            image.sprite = debugSprite; // Use debugSprite for AI cards
        }

        foreach (var card in table)
        {
            var cardObj = Instantiate(cardPrefab, tablePanel);
            var text = cardObj.GetComponentInChildren<TextMeshProUGUI>();
            text.text = card.ToString();
            text.enabled = false;
            string spriteName = $"{GetSpriteRank(card.Rank)}{GetSpriteSuit(card.Suit)}";
            Image image = cardObj.GetComponent<Image>();
            image.sprite = LoadCardSprite(spriteName);
        }
    }

    void ClearPanel(Transform panel)
    {
        foreach (Transform child in panel) Destroy(child.gameObject);
    }

    void PlayerPlayCard(Card card)
    {
        if (!isPlayerTurn || isProcessingAction) return;

        if (isPlayerAttacking)
        {
            if (table.Count == 0 || table.Any(c => c.Rank == card.Rank))
            {
                playerHand.Remove(card);
                table.Add(card);
                isPlayerTurn = false;
                UpdateStatus();
                UpdateHandUI();
                Debug.Log($"Player attacked with {card.ToString()}");
                StartCoroutine(ProcessAIRespondWithDelay());
            }
        }
        else
        {
            var attackCard = table[table.Count - 1];
            if (CanDefend(card, attackCard))
            {
                playerHand.Remove(card);
                table.Add(card);
                isPlayerTurn = false;
                UpdateStatus();
                UpdateHandUI();
                Debug.Log($"Player defended with {card.ToString()}");
                StartCoroutine(ProcessAIRespondWithDelay());
            }
        }
    }

    void OnCardClick()
    {
        if (!isPlayerTurn || isProcessingAction) return;
        Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
        RaycastHit2D hit = Physics2D.Raycast(ray.origin, ray.direction); // Corrected
        if (hit.collider != null)
        {
            var cardObj = hit.collider.gameObject;
            var card = playerHand.Find(c => c.GameObject == cardObj);
            if (card != null) PlayerPlayCard(card);
        }
    }

    bool CanDefend(Card defenseCard, Card attackCard)
    {
        if (defenseCard.IsTrump && !attackCard.IsTrump) return true;
        if (defenseCard.Suit == attackCard.Suit && defenseCard.GetRankValue() > attackCard.GetRankValue()) return true;
        return false;
    }

    void PlayerPass()
    {
        if (!isPlayerTurn || !isPlayerAttacking || isProcessingAction) return;
        Debug.Log("Player passed");
        StartCoroutine(ProcessEndTurnWithDelay());
    }

    void PlayerTake()
    {
        if (!isPlayerTurn || isPlayerAttacking || isProcessingAction) return;
        StartCoroutine(ProcessPlayerTakeWithDelay());
    }

    void AIRespond()
    {
        if (isPlayerTurn || isProcessingAction) return;

        if (isPlayerAttacking)
        {
            var attackCard = table[table.Count - 1];
            var defenseCard = aiHand.FirstOrDefault(c => CanDefend(c, attackCard));
            if (defenseCard != null)
            {
                aiHand.Remove(defenseCard);
                table.Add(defenseCard);
                isPlayerTurn = true;
                UpdateStatus();
                UpdateHandUI();
                Debug.Log($"AI defended with {defenseCard.ToString()}");
            }
            else
            {
                aiHand.AddRange(table);
                table.Clear();
                isPlayerAttacking = true;
                isPlayerTurn = true;
                UpdateStatus();
                UpdateHandUI();
                Debug.Log("AI took cards");
                StartCoroutine(ProcessDrawCardsWithDelay());
            }
        }
        else
        {
            var validRanks = table.Count == 0 ? aiHand.Select(c => c.Rank).Distinct() : table.Select(c => c.Rank).Distinct();
            var attackCard = aiHand.FirstOrDefault(c => validRanks.Contains(c.Rank) && table.Count < playerHand.Count);
            if (attackCard != null)
            {
                aiHand.Remove(attackCard);
                table.Add(attackCard);
                isPlayerTurn = true;
                UpdateStatus();
                UpdateHandUI();
                Debug.Log($"AI attacked with {attackCard.ToString()}");
            }
            else
            {
                StartCoroutine(ProcessEndTurnWithDelay());
            }
        }
    }

    void EndTurn()
    {
        table.Clear();
        isPlayerAttacking = !isPlayerAttacking;
        isPlayerTurn = isPlayerAttacking;
        UpdateStatus();
        UpdateHandUI();
        StartCoroutine(ProcessDrawCardsWithDelay());
    }

    void DrawCards()
    {
        var firstHand = isPlayerAttacking ? playerHand : aiHand;
        var secondHand = isPlayerAttacking ? aiHand : playerHand;
        while (firstHand.Count < 6 && deck.CardsRemaining() > 0) firstHand.Add(deck.DrawCard());
        while (secondHand.Count < 6 && deck.CardsRemaining() > 0) secondHand.Add(deck.DrawCard());
        UpdateHandUI();
        UpdateStatus();
        CheckWinCondition();
        if (!isPlayerTurn && !isProcessingAction) StartCoroutine(ProcessAIRespondWithDelay());
    }

    void CheckWinCondition()
    {
        if (deck.CardsRemaining() > 0 || (playerHand.Count > 0 && aiHand.Count > 0)) return;
        statusText.text = playerHand.Count == 0 ? "Player Wins!" : "AI Wins! You are the Fool!";
        enabled = false;
        Debug.Log($"Game ended: {statusText.text}");
    }

    void UpdateStatus()
    {
        if (!enabled) return;
        statusText.text = $"Trump: {trumpSuit}\nDeck: {deck.CardsRemaining()}\n{(isPlayerTurn ? "Your Turn" : "AI's Turn")}\n{(isPlayerAttacking ? "Attack" : "Defend")}";
    }

    private IEnumerator ProcessAIRespondWithDelay()
    {
        isProcessingAction = true;
        yield return new WaitForSeconds(actionDelay);
        isProcessingAction = false;
        UpdateHandUI();
        AIRespond();
    }

    private IEnumerator ProcessEndTurnWithDelay()
    {
        isProcessingAction = true;
        yield return new WaitForSeconds(actionDelay);
        isProcessingAction = false;
        UpdateHandUI();
        EndTurn();
    }

    private IEnumerator ProcessDrawCardsWithDelay()
    {
        isProcessingAction = true;
        yield return new WaitForSeconds(actionDelay);
        isProcessingAction = false;
        UpdateHandUI();
        DrawCards();
    }

    private IEnumerator ProcessPlayerTakeWithDelay()
    {
        isProcessingAction = true;
        yield return new WaitForSeconds(actionDelay);
        playerHand.AddRange(table);
        table.Clear();
        isPlayerAttacking = false;
        isPlayerTurn = false;
        UpdateStatus();
        UpdateHandUI();
        Debug.Log("Player took cards");
        isProcessingAction = false;
        StartCoroutine(ProcessDrawCardsWithDelay());
    }
}
