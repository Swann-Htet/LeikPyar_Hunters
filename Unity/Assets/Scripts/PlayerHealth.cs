using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// Put on the Person (player) object. Assign a UI Slider to show the health bar.
public class PlayerHealth : MonoBehaviour
{
    [Header("Health")]
    public int maxHealth = 100;
    public int currentHealth;

    [Header("UI")]
    public Slider healthSlider;
    [Tooltip("Optional: shown when health reaches 0")]
    public GameObject gameOverPanel;

    [Header("Events")]
    public UnityEvent onDamaged;
    public UnityEvent onDied;

    public bool IsDead => currentHealth <= 0;

    // Raised once when the player dies (the Game Over screen listens)
    public static event System.Action Died;

    void Start()
    {
        currentHealth = maxHealth;

        if (healthSlider != null)
        {
            healthSlider.minValue = 0;
            healthSlider.maxValue = maxHealth;
        }

        if (gameOverPanel != null)
            gameOverPanel.SetActive(false);

        UpdateUI();
    }

    public void TakeDamage(int amount)
    {
        if (IsDead) return;

        currentHealth = Mathf.Max(0, currentHealth - amount);
        UpdateUI();
        onDamaged?.Invoke();

        if (IsDead)
        {
            PlayerSfx.Voice("Dead");

            if (gameOverPanel != null)
                gameOverPanel.SetActive(true);

            var controller = GetComponent<PlayerController>();
            if (controller != null)
                controller.enabled = false;

            onDied?.Invoke();
            AgentStats.OnDied();
            Died?.Invoke();
        }
        else
        {
            PlayerSfx.Hurt(currentHealth, maxHealth);      // a voice at 80%, 60% and under 40% health
        }
    }

    public void Heal(int amount)
    {
        if (IsDead) return;

        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        UpdateUI();
    }

    void UpdateUI()
    {
        if (healthSlider != null)
            healthSlider.value = currentHealth;
    }
}
