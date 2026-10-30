using System.Collections.Generic;
using UnityEngine;

namespace PickaxeFall
{
    public class MiningEffects : MonoBehaviour
    {
        class Chip { public Transform transform; public SpriteRenderer renderer; public Vector2 velocity; public float life, spin; public bool tool; }
        readonly List<Chip> chips = new();
        AudioSource audioSource;
        AudioClip hitClip, rewardClip, breakClip;
        float lastSound;
        public bool Muted { get; private set; }
        public int LastShatterPieces { get; private set; }
        public int ActiveToolFragments { get { int n = 0; foreach (var chip in chips) if (chip.tool && chip.life > 0) n++; return n; } }

        public void Initialize()
        {
            Muted = PlayerPrefs.GetInt("soundMuted", 0) == 1;
            audioSource = gameObject.AddComponent<AudioSource>(); audioSource.playOnAwake = false;
            hitClip = Tone(160, 0.045f); rewardClip = Tone(660, 0.14f); breakClip = Tone(85, 0.24f);
            for (int i = 0; i < 320; i++)
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
                chip.tool = false; chip.spin = Random.Range(-180, 180);
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

        public void Shatter(Sprite sprite, Transform tool, Vector2 velocity)
        {
            LastShatterPieces = 0;
            Color32[] pixels = sprite.texture.GetPixels32();
            int width = sprite.texture.width, height = sprite.texture.height;
            for (int y = 0; y < height; y += 2) for (int x = 0; x < width; x += 2)
            {
                Color sum = Color.clear; int count = 0; Vector2 center = Vector2.zero;
                for (int dy = 0; dy < 2 && y + dy < height; dy++) for (int dx = 0; dx < 2 && x + dx < width; dx++)
                {
                    Color pixel = pixels[(y + dy) * width + x + dx];
                    if (pixel.a < 0.5f) continue;
                    sum += pixel; center += new Vector2(x + dx + 0.5f, y + dy + 0.5f); count++;
                }
                if (count == 0) continue;
                Chip piece = null;
                foreach (var chip in chips) if (chip.life <= 0) { piece = chip; break; }
                if (piece == null) break;
                Color color = sum / count; color.a = 1;
                Vector2 local = (center / count - sprite.pivot) / sprite.pixelsPerUnit;
                piece.transform.position = tool.TransformPoint(local);
                piece.transform.rotation = tool.rotation;
                piece.transform.localScale = Vector3.one * (2f / sprite.pixelsPerUnit * tool.lossyScale.x);
                piece.renderer.color = color; piece.life = Random.Range(0.8f, 1.1f);
                piece.velocity = velocity * 0.15f + Random.insideUnitCircle * 2.8f + Vector2.up * 1.6f;
                piece.spin = Random.Range(-420, 420); piece.tool = true;
                piece.transform.gameObject.SetActive(true); LastShatterPieces++;
            }
            if (!Muted) audioSource.PlayOneShot(breakClip, 0.25f);
        }

        public void Clear()
        {
            foreach (var chip in chips) { chip.life = 0; chip.tool = false; chip.transform.gameObject.SetActive(false); }
            LastShatterPieces = 0;
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
                chip.transform.Rotate(0, 0, chip.spin * Time.deltaTime);
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
        void OnDestroy() { if (hitClip) Destroy(hitClip); if (rewardClip) Destroy(rewardClip); if (breakClip) Destroy(breakClip); }
    }
}
