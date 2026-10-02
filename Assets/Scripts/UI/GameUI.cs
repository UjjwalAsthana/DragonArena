using System;
using UnityEngine;
using UnityEngine.UI;

namespace DragonBattle
{
    /// <summary>HUD: both health bars, the player's ability bar with cooldowns, countdown text and winner screen.</summary>
    public class GameUI : MonoBehaviour
    {
        [Serializable]
        public class AbilitySlot
        {
            public Image icon;
            public Image cooldownOverlay;
            public Text cooldownText;
        }

        [Header("Dragons")]
        public Dragon player;
        public Dragon enemy;

        [Header("Health bars")]
        public Image playerFill;
        public Image playerTrail;
        public Text playerHp;
        public Image enemyFill;
        public Image enemyTrail;
        public Text enemyHp;

        [Header("Abilities (order: fire, tail, fly)")]
        public AbilitySlot[] slots;

        [Header("Overlays")]
        public Text centerText;
        public GameObject winnerPanel;
        public Text winnerTitle;

        private void Start()
        {
            var controller = player.GetComponent<PlayerController>();
            if (controller == null) return;

            for (int i = 0; i < slots.Length; i++)
            {
                var button = slots[i].icon.GetComponent<Button>();
                if (button == null) continue;
                int index = i;
                button.onClick.AddListener(() => controller.UseAbility(index));
            }
        }

        private void Update()
        {
            UpdateBar(player, playerFill, playerTrail, playerHp);
            UpdateBar(enemy, enemyFill, enemyTrail, enemyHp);

            for (int i = 0; i < slots.Length; i++)
            {
                Ability a = player.Abilities[i];
                float remaining = a.Remaining;
                slots[i].cooldownOverlay.fillAmount = remaining > 0f ? remaining / a.cooldown : 0f;
                slots[i].cooldownText.text = remaining > 0f ? remaining.ToString("0.0") : string.Empty;
                slots[i].icon.color = remaining > 0f ? new Color(0.6f, 0.6f, 0.6f, 1f) : Color.white;
            }
        }

        private static void UpdateBar(Dragon dragon, Image fill, Image trail, Text label)
        {
            Health h = dragon.Health;
            float p = h.Current / h.Max;
            fill.fillAmount = p;
            trail.fillAmount = Mathf.MoveTowards(trail.fillAmount, p, Time.unscaledDeltaTime * 0.5f);
            label.text = Mathf.CeilToInt(h.Current) + " / " + Mathf.CeilToInt(h.Max);
        }

        public void ShowCenterText(string text)
        {
            centerText.gameObject.SetActive(true);
            centerText.text = text;
        }

        public void HideCenterText() => centerText.gameObject.SetActive(false);

        public void ShowWinner(string dragonName, Color color)
        {
            winnerTitle.text = dragonName.ToUpper() + (dragonName == "You" ? " WIN!" : " WINS!");
            winnerTitle.color = color;
            winnerPanel.SetActive(true);
        }
    }
}
