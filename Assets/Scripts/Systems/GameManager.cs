using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DragonBattle
{
    /// <summary>Match flow: 3-2-1 countdown, fighting, win detection, winner screen and restart.</summary>
    public class GameManager : MonoBehaviour
    {
        public Dragon player;
        public Dragon enemy;
        public GameUI ui;
        public int countdownSeconds = 3;

        private bool ended;

        private IEnumerator Start()
        {
            Time.timeScale = 1f;
            player.CanAct = false;
            enemy.CanAct = false;
            player.Health.Died += () => EndMatch(enemy);
            enemy.Health.Died += () => EndMatch(player);

            yield return new WaitForSeconds(0.5f);
            for (int i = countdownSeconds; i > 0; i--)
            {
                ui.ShowCenterText(i.ToString());
                AudioManager.Play(Sfx.Beep, 0.8f, 0f);
                yield return new WaitForSeconds(1f);
            }

            ui.ShowCenterText("FIGHT!");
            AudioManager.Play(Sfx.Go, 1f, 0f);
            player.CanAct = true;
            enemy.CanAct = true;
            yield return new WaitForSeconds(0.8f);
            ui.HideCenterText();
        }

        private void Update()
        {
            if (ended && Input.GetKeyDown(KeyCode.R)) Restart();
        }

        private void EndMatch(Dragon winner)
        {
            if (ended) return;
            ended = true;
            player.CanAct = false;
            enemy.CanAct = false;
            StartCoroutine(EndSequence(winner));
        }

        private IEnumerator EndSequence(Dragon winner)
        {
            Time.timeScale = 0.25f; // slow motion on the killing blow
            yield return new WaitForSecondsRealtime(1.2f);
            Time.timeScale = 1f;
            AudioManager.Play(Sfx.Win, 1f, 0f);
            ui.ShowWinner(winner.displayName, winner.themeColor);
        }

        public void Restart()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}
