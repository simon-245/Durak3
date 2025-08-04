/*using UnityEngine;
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
*/

using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Microsoft.ML.OnnxRuntime;           // Main ONNX Runtime namespace
using Microsoft.ML.OnnxRuntime.Tensors;   // Namespace for DenseTensor

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
    [SerializeField] private Button passButton; // Readded
    [SerializeField] private Button takeButton; // Readded

    private float actionDelay = 3f;

    private PlayerControls controls;
    private InferenceSession session; // ONNX model session

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
        LoadModel();
        UpdateStatus();
        Debug.Log($"Initial state: isPlayerTurn={isPlayerTurn}, isPlayerAttacking={isPlayerAttacking}, Deck={deck.CardsRemaining()}, Trump={trumpSuit}");
    }

    void LoadModel()
    {
        try
        {
            session = new InferenceSession("Assets/Models/ppo_durak.onnx");
            Debug.Log("ONNX model loaded successfully.");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to load ONNX model: {e.Message}\n{e.StackTrace}");
        }
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
        if (isPlayerTurn || isProcessingAction || session == null)
        {
            Debug.LogWarning($"AIRespond blocked: isPlayerTurn={isPlayerTurn}, isProcessingAction={isProcessingAction}, session={session != null}");
            return;
        }

        Debug.Log($"AIRespond called: isPlayerTurn={isPlayerTurn}, isPlayerAttacking={isPlayerAttacking}, table.Count={table.Count}");

        float[] inputState = GetGameStateAsInput();
        Debug.Log($"Input state: {string.Join(", ", inputState.Select(v => v.ToString("F2")))}");
        var inputTensor = new DenseTensor<float>(inputState, new[] { 1, 79 });
        var inputs = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor("input", inputTensor) };

        bool actionTaken = false;
        try
        {
            using (var outputs = session.Run(inputs))
            {
                float[] actionProbs = outputs.First().AsTensor<float>().ToArray();
                Debug.Log($"Action probs: {string.Join(", ", actionProbs.Select(p => p.ToString("F4")))}");
                int actionIndex = GetMaxIndex(actionProbs);
                Debug.Log($"AI chose action index: {actionIndex}");

                if (isPlayerAttacking)
                {
                    var defendableCards = aiHand.Where(c => table.Count > 0 && CanDefend(c, table[table.Count - 1])).ToList();
                    Debug.Log($"Defendable cards count: {defendableCards.Count}, cards: {string.Join(", ", defendableCards.Select(c => c.ToString()))}");
                    if (defendableCards.Count > 0)
                    {
                        actionIndex = Mathf.Min(actionIndex, defendableCards.Count - 1); // Cap index
                        Debug.Log($"Adjusted actionIndex: {actionIndex}");
                        var defenseCard = defendableCards[actionIndex];
                        if (defenseCard != null)
                        {
                            aiHand.Remove(defenseCard);
                            table.Add(defenseCard);
                            isPlayerTurn = true; // Switch back to player after defense
                            UpdateStatus();
                            UpdateHandUI();
                            Debug.Log($"AI defended with {defenseCard.ToString()}");
                            actionTaken = true;
                        }
                    }
                    if (!actionTaken && defendableCards.Count > 0)
                    {
                        // Fallback: Random defense if model choice fails
                        var randomDefense = defendableCards[Random.Range(0, defendableCards.Count)];
                        aiHand.Remove(randomDefense);
                        table.Add(randomDefense);
                        isPlayerTurn = true;
                        UpdateStatus();
                        UpdateHandUI();
                        Debug.Log($"AI defended with random fallback: {randomDefense.ToString()}");
                        actionTaken = true;
                    }
                    else if (defendableCards.Count == 0)
                    {
                        // No defendable cards, take them
                        aiHand.AddRange(table);
                        table.Clear();
                        isPlayerAttacking = true;
                        isPlayerTurn = true;
                        UpdateStatus();
                        UpdateHandUI();
                        Debug.Log("AI took cards (no defendable cards)");
                        StartCoroutine(ProcessDrawCardsWithDelay());
                        actionTaken = true;
                    }
                }
                else
                {
                    var validRanks = table.Count == 0 ? aiHand.Select(c => c.Rank).Distinct() : table.Select(c => c.Rank).Distinct();
                    Debug.Log($"Valid ranks for attack: {string.Join(", ", validRanks)}");
                    if (actionIndex < aiHand.Count)
                    {
                        var attackCard = aiHand[actionIndex];
                        if (validRanks.Contains(attackCard.Rank) && table.Count < playerHand.Count)
                        {
                            aiHand.Remove(attackCard);
                            table.Add(attackCard);
                            isPlayerTurn = true; // Switch back to player after attack
                            UpdateStatus();
                            UpdateHandUI();
                            Debug.Log($"AI attacked with {attackCard.ToString()}");
                            actionTaken = true;
                        }
                    }
                    if (!actionTaken && aiHand.Count > 0)
                    {
                        // Fallback: Random attack if model choice fails
                        var randomAttack = aiHand[Random.Range(0, aiHand.Count)];
                        if (validRanks.Contains(randomAttack.Rank) && table.Count < playerHand.Count)
                        {
                            aiHand.Remove(randomAttack);
                            table.Add(randomAttack);
                            isPlayerTurn = true;
                            UpdateStatus();
                            UpdateHandUI();
                            Debug.Log($"AI attacked with random fallback: {randomAttack.ToString()}");
                            actionTaken = true;
                        }
                    }
                    if (!actionTaken)
                    {
                        // Fallback: Pass if no attack possible
                        Debug.LogWarning("AI found no valid attack, forcing pass");
                        StartCoroutine(ProcessEndTurnWithDelay());
                        actionTaken = true;
                    }
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"AI inference failed: {e.Message}\n{e.StackTrace}");
            // Fallback: Take cards or pass if inference fails
            if (isPlayerAttacking)
            {
                aiHand.AddRange(table);
                table.Clear();
                isPlayerAttacking = true;
                isPlayerTurn = true;
                UpdateStatus();
                UpdateHandUI();
                Debug.Log("AI took cards (inference failed fallback)");
                StartCoroutine(ProcessDrawCardsWithDelay());
                actionTaken = true;
            }
            else
            {
                StartCoroutine(ProcessEndTurnWithDelay());
                actionTaken = true;
            }
        }

        if (!actionTaken)
        {
            Debug.LogError("AI failed to take any action, forcing pass as last resort");
            StartCoroutine(ProcessEndTurnWithDelay());
        }
    }

    private float[] GetGameStateAsInput()
    {
        float[] state = new float[79];
        int index = 0;

        // Encode player hand
        for (int i = 0; i < 6 && i < playerHand.Count; i++)
        {
            state[index++] = playerHand[i].GetRankValue() / 13f;
            state[index++] = EncodeSuit(playerHand[i].Suit);
            state[index++] = playerHand[i].IsTrump ? 1f : 0f;
            state[index++] = (float)i / 5f;
        }
        while (index < 24) state[index++] = 0f;

        // Encode AI hand
        for (int i = 0; i < 6 && i < aiHand.Count; i++)
        {
            state[index++] = aiHand[i].GetRankValue() / 13f;
            state[index++] = EncodeSuit(aiHand[i].Suit);
            state[index++] = aiHand[i].IsTrump ? 1f : 0f;
            state[index++] = (float)i / 5f;
        }
        while (index < 48) state[index++] = 0f;

        // Encode table
        for (int i = 0; i < 6 && i < table.Count; i++)
        {
            state[index++] = table[i].GetRankValue() / 13f;
            state[index++] = EncodeSuit(table[i].Suit);
            state[index++] = table[i].IsTrump ? 1f : 0f;
        }
        while (index < 66) state[index++] = 0f;

        // Game state
        state[index++] = isPlayerTurn ? 1f : 0f;
        state[index++] = isPlayerAttacking ? 1f : 0f;
        state[index++] = (float)deck.CardsRemaining() / 36f;
        state[index++] = EncodeSuit(trumpSuit);
        state[index++] = (float)Mathf.Min(table.Count, 6) / 6f;
        state[index++] = (float)playerHand.Count / 6f;

        while (index < 79) state[index++] = 0f;
        return state;
    }

    private float EncodeSuit(string suit)
    {
        return suit switch
        {
            "Spades" => 0f,
            "Clubs" => 0.33f,
            "Diamonds" => 0.67f,
            "Hearts" => 1f,
            _ => 0f
        };
    }

    private int GetMaxIndex(float[] probs)
    {
        int maxIndex = 0;
        float maxValue = probs[0];
        for (int i = 1; i < probs.Length; i++)
        {
            if (probs[i] > maxValue)
            {
                maxValue = probs[i];
                maxIndex = i;
            }
        }
        return maxIndex;
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
        Debug.Log("Starting AI response delay");
        yield return new WaitForSeconds(actionDelay);
        isProcessingAction = false;
        Debug.Log("AI response delay completed, calling AIRespond");
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