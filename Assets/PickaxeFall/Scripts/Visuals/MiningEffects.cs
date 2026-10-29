using System.Collections.Generic;
using UnityEngine;

namespace PickaxeFall
{
    public class MiningEffects : MonoBehaviour
    {
        class Chip { public Transform transform; public SpriteRenderer renderer; public Vector2 velocity; public float life; }
        readonly List<Chip> chips = new();
        AudioSource audioSource;
        AudioClip hitClip, rewardClip;
        float lastSound;
        public bool Muted { get; private set; }

        public void Initialize()
        {
            Muted = PlayerPrefs.GetInt("soundMuted", 0) == 1;
            audioSource = gameObject.AddComponent<AudioSource>(); audioSource.playOnAwake = false;
            hitClip = Tone(160, 0.045f); rewardClip = Tone(660, 0.14f);
            for (int i = 0; i < 72; i++)
            {
                var chip = new GameObject("Pooled chip"); chip.transform.SetParent(transform);
                var renderer = chip.AddComponent<SpriteRenderer>(); renderer.sprite = PixelArt.Square; renderer.sortingOrder = 15;
                chip.SetActive(false); chips.Add(new Chip { transform = chip.transform, renderer = renderer });
            }
        }

        public void ToggleSound()
        {
            Muted = !Muted; PlayerPrefs.SetInt("soundMuted", Muted ? 1 : 0); PlayerPrefs.Save();
        }

        public void Impact(Vector2 point, Color color, int count, bool reward = false)
        {
            int emitted = 0;
            foreach (var chip in chips)
            {
                if (chip.life > 0) continue;
                chip.life = Random.Range(0.22f, 0.55f);
                chip.transform.gameObject.SetActive(true); chip.transform.position = point;
                chip.transform.localScale = Vector3.one * Random.Range(0.055f, 0.13f);
                chip.renderer.color = color;
                chip.velocity = new Vector2(Random.Range(-2.5f, 2.5f), Random.Range(1, 3));
                if (++emitted >= count) break;
            }
            if (!Muted && Time.time > lastSound + 0.07f)
            {
                lastSound = Time.time; audioSource.pitch = Random.Range(0.9f, 1.12f);
                audioSource.PlayOneShot(reward ? rewardClip : hitClip, reward ? 0.13f : 0.06f);
            }
        }

        public void Explosion(Vector2 point, float radius)
        {
            Impact(point, new Color32(255, 192, 78, 255), 30, true);
            foreach (var chip in chips)
            {
                if (chip.life <= 0) continue;
                if (((Vector2)chip.transform.position - point).sqrMagnitude > 0.01f) continue;
                chip.velocity *= radius * 1.3f;
                chip.transform.localScale *= 1.6f;
            }
        }

        void Update()
        {
            foreach (var chip in chips)
            {
                if (chip.life <= 0) continue;
                chip.life -= Time.deltaTime;
                if (chip.life <= 0) { chip.transform.gameObject.SetActive(false); continue; }
                chip.velocity += Vector2.down * (8 * Time.deltaTime);
                chip.transform.position += (Vector3)(chip.velocity * Time.deltaTime);
            }
        }

        static AudioClip Tone(float frequency, float duration)
        {
            const int rate = 22050;
            float[] samples = new float[(int)(rate * duration)];
            for (int i = 0; i < samples.Length; i++)
                samples[i] = Mathf.Sin(2 * Mathf.PI * frequency * i / rate) * (1f - (float)i / samples.Length);
            var clip = AudioClip.Create("Mining tone", samples.Length, 1, rate, false); clip.SetData(samples, 0); return clip;
        }
        void OnDestroy() { if (hitClip) Destroy(hitClip); if (rewardClip) Destroy(rewardClip); }
    }
}
