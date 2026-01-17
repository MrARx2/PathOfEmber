using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Singleton manager for spawning and pooling coins.
/// Handles tiered spawn patterns: instant burst for regular enemies, fountain for bosses.
/// </summary>
public class CoinManager : MonoBehaviour
{
    public static CoinManager Instance { get; private set; }
    
    [Header("Coin Prefab")]
    [SerializeField] private GameObject coinPrefab;
    
    [Header("Pool Settings")]
    [SerializeField] private int initialPoolSize = 50;
    
    [Header("Spawn Tiers")]
    [SerializeField, Tooltip("XP threshold for quick fountain (1 second)")]
    private int quickFountainThreshold = 31;
    [SerializeField, Tooltip("XP threshold for full fountain (2 seconds)")]
    private int fullFountainThreshold = 61;
    
    [Header("Coin Count Limits")]
    [SerializeField] private int maxBurstCoins = 10;
    [SerializeField] private int maxQuickFountainCoins = 15;
    [SerializeField] private int maxFullFountainCoins = 20;
    
    [Header("Fountain Settings")]
    [SerializeField] private float quickFountainDuration = 1f;
    [SerializeField] private float fullFountainDuration = 2f;
    [SerializeField] private int coinsPerWave = 3;
    
    [Header("Debug")]
    [SerializeField] private bool debugLog = false;
    
    private Queue<Coin> coinPool = new Queue<Coin>();
    private Transform playerTransform;
    private Transform poolContainer;
    
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        
        // Create container for pooled coins
        poolContainer = new GameObject("CoinPool").transform;
        poolContainer.SetParent(transform);
    }
    
    private void Start()
    {
        FindPlayer();
        InitializePool();
    }
    
    private void FindPlayer()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerTransform = player.transform;
        }
    }
    
    private void InitializePool()
    {
        if (coinPrefab == null)
        {
            Debug.LogError("[CoinManager] Coin prefab not assigned!");
            return;
        }
        
        for (int i = 0; i < initialPoolSize; i++)
        {
            CreatePooledCoin();
        }
        
        if (debugLog)
            Debug.Log($"[CoinManager] Initialized pool with {initialPoolSize} coins");
    }
    
    private Coin CreatePooledCoin()
    {
        GameObject coinObj = Instantiate(coinPrefab, poolContainer);
        coinObj.SetActive(false);
        Coin coin = coinObj.GetComponent<Coin>();
        coinPool.Enqueue(coin);
        return coin;
    }
    
    private Coin GetCoin()
    {
        if (coinPool.Count == 0)
        {
            return CreatePooledCoin();
        }
        return coinPool.Dequeue();
    }
    
    /// <summary>
    /// Return a coin to the pool after collection.
    /// </summary>
    public void ReturnToPool(Coin coin)
    {
        coin.ResetCoin();
        coinPool.Enqueue(coin);
    }
    
    /// <summary>
    /// Spawn coins at position based on XP amount.
    /// Automatically chooses spawn pattern based on XP tier.
    /// </summary>
    public void SpawnCoins(Vector3 position, int totalXP)
    {
        if (coinPrefab == null || playerTransform == null) return;
        if (totalXP <= 0) return;
        
        // Determine spawn pattern and coin count
        int coinCount;
        float duration;
        
        if (totalXP >= fullFountainThreshold)
        {
            // Full fountain (2s) - bosses
            coinCount = CalculateCoinCount(totalXP, maxFullFountainCoins);
            duration = fullFountainDuration;
            
            if (debugLog)
                Debug.Log($"[CoinManager] Full fountain: {coinCount} coins over {duration}s for {totalXP} XP");
        }
        else if (totalXP >= quickFountainThreshold)
        {
            // Quick fountain (1s)
            coinCount = CalculateCoinCount(totalXP, maxQuickFountainCoins);
            duration = quickFountainDuration;
            
            if (debugLog)
                Debug.Log($"[CoinManager] Quick fountain: {coinCount} coins over {duration}s for {totalXP} XP");
        }
        else
        {
            // Instant burst
            coinCount = CalculateCoinCount(totalXP, maxBurstCoins);
            duration = 0f;
            
            if (debugLog)
                Debug.Log($"[CoinManager] Instant burst: {coinCount} coins for {totalXP} XP");
        }
        
        // Calculate XP per coin
        int baseXPPerCoin = totalXP / coinCount;
        int remainderXP = totalXP % coinCount;
        
        if (duration > 0f)
        {
            StartCoroutine(FountainSpawnRoutine(position, coinCount, baseXPPerCoin, remainderXP, duration));
        }
        else
        {
            SpawnCoinBurst(position, coinCount, baseXPPerCoin, remainderXP);
        }
    }
    
    private int CalculateCoinCount(int xp, int maxCoins)
    {
        // Base formula: 1 coin per 3.5 XP
        int count = Mathf.RoundToInt(xp / 3.5f);
        return Mathf.Clamp(count, 1, maxCoins);
    }
    
    private void SpawnCoinBurst(Vector3 position, int count, int baseXP, int remainderXP)
    {
        for (int i = 0; i < count; i++)
        {
            // First coin gets remainder XP
            int xpValue = baseXP + (i == 0 ? remainderXP : 0);
            
            // Random explosion direction (360 degrees)
            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            Vector3 dir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            
            SpawnSingleCoin(position, xpValue, dir);
        }
    }
    
    private IEnumerator FountainSpawnRoutine(Vector3 position, int totalCoins, int baseXP, int remainderXP, float duration)
    {
        int coinsSpawned = 0;
        int waveCount = Mathf.CeilToInt((float)totalCoins / coinsPerWave);
        float waveInterval = duration / waveCount;
        
        for (int wave = 0; wave < waveCount; wave++)
        {
            int coinsThisWave = Mathf.Min(coinsPerWave, totalCoins - coinsSpawned);
            
            for (int i = 0; i < coinsThisWave; i++)
            {
                // First coin gets remainder XP
                int xpValue = baseXP + (coinsSpawned == 0 ? remainderXP : 0);
                
                // Fountain direction: upward with spread
                float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
                float spreadRadius = Random.Range(0.3f, 0.6f);
                Vector3 dir = new Vector3(
                    Mathf.Cos(angle) * spreadRadius,
                    1f, // Strong upward
                    Mathf.Sin(angle) * spreadRadius
                ).normalized;
                
                SpawnSingleCoin(position, xpValue, dir);
                coinsSpawned++;
            }
            
            if (wave < waveCount - 1)
            {
                yield return new WaitForSeconds(waveInterval);
            }
        }
    }
    
    private void SpawnSingleCoin(Vector3 position, int xpValue, Vector3 explosionDir)
    {
        Coin coin = GetCoin();
        coin.transform.position = position;
        coin.Initialize(xpValue, explosionDir, playerTransform);
    }
}
