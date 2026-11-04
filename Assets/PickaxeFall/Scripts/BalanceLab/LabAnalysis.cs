#if BALANCE_LAB || UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;

namespace PickaxeFall.BalanceLab
{
    public sealed class LabSummary
    {
        public LabScenario scenario;
        public List<LabResult> rows;
        public int Count => rows.Count;
        public double Mean(Func<LabResult,double> get) => rows.Count == 0 ? double.NaN : rows.Average(get);
        public double Rate => LabAnalysis.Rate(rows);
        public double Median(Func<LabResult,double> get) { var a = rows.Select(get).OrderBy(x => x).ToArray(); return a.Length == 0 ? double.NaN : (a[(a.Length-1)/2]+a[a.Length/2])/2; }
        public double Min(Func<LabResult,double> get) => rows.Count == 0 ? double.NaN : rows.Min(get);
        public double Max(Func<LabResult,double> get) => rows.Count == 0 ? double.NaN : rows.Max(get);
    }
    public sealed class LabAnalysis
    {
        readonly LabSession session;
        readonly int[,] pointLevels = new int[3,25];
        readonly Dictionary<(int,int),LabPrice> prices;
        readonly Dictionary<string,LabScenario> scenarios;
        readonly Dictionary<string,List<LabResult>> grouped;
        readonly Dictionary<string,LabSummary> summaries = new();
        public LabAnalysis(LabSession source)
        {
            session = source;
            prices=(source.Manifest.prices ?? Array.Empty<LabPrice>()).ToDictionary(p=>(p.key,p.level));
            foreach(var scenario in source.Scenarios) for(int i=0;i<25;i++) pointLevels[scenario.point,i]=Math.Max(pointLevels[scenario.point,i],scenario.levels[i]);
            scenarios = source.Scenarios.ToDictionary(s => s.id);
            grouped = source.Results.Values.Where(r => string.IsNullOrEmpty(r.error)).GroupBy(r => r.scenario).ToDictionary(g => g.Key,g => g.OrderBy(r => r.seed).ToList());
        }
        public static double Rate(IList<LabResult> rows) => rows.Count == 0 ? double.NaN : 60 * rows.Sum(r => r.value) / (rows.Sum(r => r.seconds) + rows.Count * 8);
        public LabSummary Get(LabScenario scenario)
        {
            if (summaries.TryGetValue(scenario.id,out var saved)) return saved;
            List<LabResult> rows;
            if (scenario.tier >= 0) { if (!grouped.TryGetValue(scenario.id,out rows)) rows = new(); }
            else rows = Weighted(scenario);
            return summaries[scenario.id] = new LabSummary { scenario = scenario, rows = rows };
        }
        List<LabResult> Weighted(LabScenario scenario)
        {
            if (!session.Plan.tiers.All(x => x)) return new();
            var sources = new List<Dictionary<int,LabResult>>();
            for (int tier = 0; tier < 5; tier++)
            {
                int allowed = Progression.Keys.Where(k => k.Kind > UpgradeKind.Durability || k.Tier >= tier).Aggregate(0,(a,k) => a | (1 << k.Index));
                string id = $"t{tier}-p{scenario.point}-b{scenario.background}-a{(scenario.active ? 1 : 0)}-m{(session.Plan.mode == 3 ? scenario.mask : scenario.mask & allowed)}";
                if (!grouped.TryGetValue(id,out var rows)) return new();
                sources.Add(rows.ToDictionary(r => r.seed));
            }
            var result = new List<LabResult>();
            foreach (int seed in sources[0].Keys.Where(seed => sources.All(s => s.ContainsKey(seed))))
            {
                var row = new LabResult { id = scenario.id+"-"+seed, scenario = scenario.id, seed = seed };
                for (int tier = 0; tier < 5; tier++)
                {
                    var x = sources[tier][seed]; double w = session.Manifest.roulette[tier];
                    row.seconds += w*x.seconds; row.depth += w*x.depth; row.value += w*x.value; row.blocks += w*x.blocks;
                    row.strikes += w*x.strikes; row.tools += w*x.tools; row.benches += w*x.benches; row.abilities += w*x.abilities; row.censored += w*x.censored;
                    for (int i = 0; i < ResourceInventory.SlotCount; i++) row.resources[i] += w*x.resources[i];
                    for (int i = 0; i < 17; i++) { row.attempts[i] += w*x.attempts[i]; row.successes[i] += w*x.successes[i]; }
                }
                result.Add(row);
            }
            return result.OrderBy(r => r.seed).ToList();
        }
        public List<LabSummary> View(int tier, int point, int background, bool active)
        {
            var templates = session.Scenarios.Where(s => s.point == point && s.background == background && s.active == active && (tier < 0 || s.tier == tier)).ToList();
            if (tier >= 0) return templates.Select(Get).ToList();
            var masks = new HashSet<int>(templates.Select(s => s.mask));
            if (session.Plan.mode == 1 || session.Plan.mode == 2)
            {
                for (int i = 0; i < 25; i++) for (int j = i+1; j < 25; j++)
                {
                    masks.Add((1<<i)|(1<<j));
                    if (session.Plan.mode == 2) for (int k=j+1;k<25;k++) masks.Add((1<<i)|(1<<j)|(1<<k));
                }
            }
            if (templates.Count == 0) return new();
            return masks.Select(mask =>
            {
                int[] levels = new int[25];
                foreach (var key in Progression.Keys) if (((mask & (1 << key.Index)) != 0) != (background == 1)) levels[key.Index] = session.Plan.mode == 3 ? session.Plan.customLevels[key.Index] : pointLevels[point,key.Index];
                return Get(new LabScenario { id = $"t-1-p{point}-b{background}-a{(active ? 1 : 0)}-m{mask}", tier = -1, point = point, background = background, active = active, mask = mask, order = LabPlanner.Bits(mask), levels = levels });
            }).ToList();
        }
        public LabSummary Related(LabSummary item, int mask)
        {
            var s = item.scenario; string id = s.Family+"-m"+mask;
            if (s.tier >= 0) return scenarios.TryGetValue(id,out var found) ? Get(found) : null;
            int[] levels = new int[25];
            foreach (var key in Progression.Keys) if (((mask & (1 << key.Index)) != 0) != (s.background == 1)) levels[key.Index] = session.Plan.mode == 3 ? session.Plan.customLevels[key.Index] : pointLevels[s.point,key.Index];
            return Get(new LabScenario { id = id, tier = -1, point = s.point, background = s.background, active = s.active, mask = mask, order = LabPlanner.Bits(mask), levels = levels });
        }
        public double Delta(LabSummary item)
        {
            var baseline = Related(item,0); if (baseline == null) return double.NaN;
            var (a,b) = Paired(item,baseline); double delta = Rate(a)-Rate(b);
            return item.scenario.background == 1 ? -delta : delta;
        }
        public static (List<LabResult>,List<LabResult>) Paired(LabSummary a, LabSummary b)
        {
            var map = b.rows.ToDictionary(r => r.seed);
            var first = a.rows.Where(r => map.ContainsKey(r.seed)).ToList(); return (first,first.Select(r => map[r.seed]).ToList());
        }
        public (double low,double high,int count) Interval(LabSummary item)
        {
            var baseline = Related(item,0); if (baseline == null) return (double.NaN,double.NaN,0);
            var (a,b) = Paired(item,baseline); if (a.Count < 8) return (double.NaN,double.NaN,a.Count);
            double[] values = new double[1000]; var random = new Random(8127);
            for (int draw = 0; draw < values.Length; draw++)
            {
                double av = 0, at = 0, bv = 0, bt = 0;
                for (int i = 0; i < a.Count; i++) { int k = random.Next(a.Count); av += a[k].value; at += a[k].seconds+8; bv += b[k].value; bt += b[k].seconds+8; }
                values[draw] = (60*av/at-60*bv/bt)*(item.scenario.background == 1 ? -1 : 1);
            }
            Array.Sort(values); return (values[24],values[974],a.Count);
        }
        public double Interaction(LabSummary item)
        {
            if (item.scenario.order != 2) return double.NaN;
            int a = item.scenario.mask & -item.scenario.mask, b = item.scenario.mask ^ a;
            var groups = new[] { item,Related(item,a),Related(item,b),Related(item,0) };
            if (groups.Any(g => g == null)) return double.NaN;
            var maps = groups.Select(g => g.rows.ToDictionary(r => r.seed)).ToArray();
            var seeds = maps[0].Keys.Where(seed => maps.All(m => m.ContainsKey(seed))).ToArray();
            if (seeds.Length == 0) return double.NaN;
            var rates = maps.Select(m => Rate(seeds.Select(seed => m[seed]).ToList())).ToArray();
            return rates[0]-rates[1]-rates[2]+rates[3];
        }
        public (double low,double high,int count) InteractionInterval(LabSummary item)
        {
            if (item.scenario.order!=2) return (double.NaN,double.NaN,0);
            int a=item.scenario.mask & -item.scenario.mask, b=item.scenario.mask^a;
            var groups=new[] { item,Related(item,a),Related(item,b),Related(item,0) };
            if (groups.Any(g=>g==null)) return (double.NaN,double.NaN,0);
            var maps=groups.Select(g=>g.rows.ToDictionary(r=>r.seed)).ToArray();
            var seeds=maps[0].Keys.Where(seed=>maps.All(m=>m.ContainsKey(seed))).ToArray();
            if (seeds.Length<8) return (double.NaN,double.NaN,seeds.Length);
            var values=new double[1000]; var random=new Random(8127);
            for (int draw=0;draw<values.Length;draw++)
            {
                var v=new double[4];var t=new double[4];
                for (int i=0;i<seeds.Length;i++) { int seed=seeds[random.Next(seeds.Length)]; for(int g=0;g<4;g++) { var r=maps[g][seed];v[g]+=r.value;t[g]+=r.seconds+8; } }
                values[draw]=60*(v[0]/t[0]-v[1]/t[1]-v[2]/t[2]+v[3]/t[3]);
            }
            Array.Sort(values);return(values[24],values[974],seeds.Length);
        }
        public double TripleGain(LabSummary item)
        {
            if (item.scenario.order != 3) return double.NaN;
            var pairs = Progression.Keys.Where(k => (item.scenario.mask & (1 << k.Index)) != 0).Select(k => Related(item,item.scenario.mask ^ (1 << k.Index))).Where(x => x != null && x.Count > 0).ToList();
            var baseline=Related(item,0); if(pairs.Count!=3 || baseline==null)return double.NaN;
            var groups=new[] { item,baseline }.Concat(pairs).Select(g=>g.rows.ToDictionary(r=>r.seed)).ToArray();
            var seeds=groups[0].Keys.Where(seed=>groups.All(g=>g.ContainsKey(seed))).ToArray(); if(seeds.Length==0)return double.NaN;
            var rates=groups.Select(g=>Rate(seeds.Select(seed=>g[seed]).ToList())).ToArray();
            double sign=item.scenario.background==1 ? -1 : 1;
            return sign*(rates[0]-rates[1])-rates.Skip(2).Max(rate=>sign*(rate-rates[1]));
        }
        public (long coins,long[] materials,double total) Cost(LabSummary item)
        {
            long coins = 0; long[] materials = new long[ResourceInventory.SlotCount];
            foreach (var key in Progression.Keys.Where(k => (item.scenario.mask & (1 << k.Index)) != 0))
            {
                int level = pointLevels[item.scenario.point,key.Index];
                if(level==0)continue;
                if(!prices.TryGetValue((key.Index,level),out var price) || session.Manifest.sellPrices==null) return (-1,materials,double.NaN);
                coins+=price.coins;for(int i=0;i<Math.Min(ResourceInventory.SlotCount,price.materials.Length);i++)materials[i]+=price.materials[i];
            }
            return (coins,materials,coins+(session.Manifest.sellPrices==null ? 0 : materials.Select((x,i) => i<session.Manifest.sellPrices.Length?(double)x*session.Manifest.sellPrices[i]:0).Sum()));
        }
        public string Reliability(LabSummary item)
        {
            if (session.Plan.diagnostic) return "ДИАГНОСТИКА: принудительные эффекты, без оценки окупаемости";
            var notes = new List<string>();
            if(session.Manifest.chanceSteps==null)notes.Add("старый формат: нет снимка экономики и вероятностей");
            if (item.Count < 8) notes.Add("мало карт (<8)");
            if (item.Mean(r => r.censored) > 0) notes.Add("есть ограниченные забеги");
            if (!item.scenario.active && item.scenario.levels.Skip(18).Take(5).Any(x => x > 0)) notes.Add("энергия/приёмы без жестов не используются");
            int size = new UpgradeKey(UpgradeKind.TeamSize).Index, chance = new UpgradeKey(UpgradeKind.TeamChance).Index;
            if (item.scenario.background == 0 && item.scenario.levels[size] > 0 && item.scenario.levels[chance] == 0) notes.Add("размер отряда требует шанса отряда");
            foreach (var kind in new[] { UpgradeKind.Fortune,UpgradeKind.Careful,UpgradeKind.TntFind,UpgradeKind.DoubleBench,UpgradeKind.DoubleBlast })
            {
                int index = new UpgradeKey(kind).Index;
                if ((item.scenario.mask & (1 << index)) == 0) continue;
                var reference = item.scenario.background == 1 ? Related(item,0) : item;
                double expected = reference.rows.Sum(r => r.attempts[(int)kind]) * pointLevels[item.scenario.point,index] * (session.Manifest.chanceSteps==null ? 0 : session.Manifest.chanceSteps[(int)kind]);
                double observed = reference.rows.Sum(r => r.successes[(int)kind]);
                if (expected < 20 || observed < 10) notes.Add(Progression.Title(kind)+": мало срабатываний");
            }
            return notes.Count == 0 ? "Смотри интервал и разброс; причинность ограничена выбранной стратегией" : string.Join("; ",notes);
        }
        public string Detail(LabSummary item)
        {
            var ci = Interval(item); var cost = Cost(item); double delta = Delta(item);
            var text = new StringBuilder(item.scenario.Title+"\n");
            text.AppendLine($"Карт: {item.Count}; Δ монет/мин: {LabFiles.Number(delta)}; 95% интервал: [{LabFiles.Number(ci.low)}; {LabFiles.Number(ci.high)}], пар: {ci.count}");
            text.AppendLine($"Глубина: среднее {LabFiles.Number(item.Mean(r => r.depth))}, медиана {LabFiles.Number(item.Median(r => r.depth))}, диапазон {LabFiles.Number(item.Min(r => r.depth))}–{LabFiles.Number(item.Max(r => r.depth))} м");
            text.AppendLine($"Время: среднее {LabFiles.Number(item.Mean(r => r.seconds))}, медиана {LabFiles.Number(item.Median(r => r.seconds))}, диапазон {LabFiles.Number(item.Min(r => r.seconds))}–{LabFiles.Number(item.Max(r => r.seconds))} с; ограничено {LabFiles.Number(100*item.Mean(r => r.censored))}%");
            foreach(var kind in ResourceInventory.Materials){int index=(int)kind;text.AppendLine(GameBalance.BlockName(kind)+": "+LabFiles.Number(item.Mean(r=>r.resources[index])));}
            text.AppendLine($"Блоки {LabFiles.Number(item.Mean(r => r.blocks))}; удары {LabFiles.Number(item.Mean(r => r.strikes))}; кирки {LabFiles.Number(item.Mean(r => r.tools))}; верстаки {LabFiles.Number(item.Mean(r => r.benches))}; действия {LabFiles.Number(item.Mean(r => r.abilities))}");
            text.AppendLine("Цена: "+(cost.coins<0 ? "нет снимка цен в старом отчёте" : cost.coins.ToString())+" монет; материалы: "+string.Join(", ",cost.materials.Select((x,i) => x>0 ? GameBalance.BlockName((BlockKind)i)+" "+x : "").Where(x => x.Length>0)));
            text.AppendLine("Полная стоимость с продажной ценой материалов: "+LabFiles.Number(cost.total));
            text.AppendLine("Условная окупаемость, мин: "+(!session.Plan.diagnostic && ci.low > 0 && delta > 0 ? LabFiles.Number(cost.total/delta) : "не определена: прибавка неположительная или недостаточно данных"));
            text.AppendLine("Взаимодействие пары, монет/мин: "+LabFiles.Number(Interaction(item)));
            var interactionCi=InteractionInterval(item); if(item.scenario.order==2)text.AppendLine("95% интервал взаимодействия: ["+LabFiles.Number(interactionCi.low)+"; "+LabFiles.Number(interactionCi.high)+"], общих карт "+interactionCi.count);
            text.AppendLine("Тройка сверх лучшей пары (по потерям на общем фоне): "+LabFiles.Number(TripleGain(item)));
            foreach (var kind in Enum.GetValues(typeof(UpgradeKind)).Cast<UpgradeKind>().Where(k => Progression.ChanceStep(k)>0)) text.AppendLine(Progression.Title(kind)+": попытки "+LabFiles.Number(item.rows.Sum(r => r.attempts[(int)kind]))+", срабатывания "+LabFiles.Number(item.rows.Sum(r => r.successes[(int)kind])));
            text.AppendLine(Reliability(item)); return text.ToString();
        }
        public void Export(string directory)
        {
            var csv = new StringBuilder("scenario,tier,point,background,active,mask,samples,depthMean,depthMedian,depthMin,depthMax,secondsMean,secondsMedian,valueMean,coinsPerMinute,deltaRate,ciLow,ciHigh,blocks,tools,benches,abilities,censored,costCoins,costEquivalent,paybackMinutes,interaction,tripleGain,dirt,stone,coal,copper,iron,gold,diamond,deepstone,emerald,lapis,redstone,amethyst,notes\n");
            var html = new StringBuilder("<!doctype html><html lang='ru'><meta charset='utf-8'><meta name='viewport' content='width=device-width'><title>PickaxeFall Balance Lab</title><style>body{font:15px system-ui;background:#172128;color:#eee;margin:24px}table{border-collapse:collapse}td,th{padding:8px;border:1px solid #53616a;text-align:right}td:first-child{text-align:left}input{padding:10px;width:400px}summary{cursor:pointer}small{color:#bbb}</style><h1>PickaxeFall Balance Lab</h1>");
            html.Append("<p>Версия "+WebUtility.HtmlEncode(session.Manifest.version)+" · Unity "+WebUtility.HtmlEncode(session.Manifest.unity)+" · "+session.Results.Count+" / "+session.Total+" физических сценариев-карт. Отпечаток: "+session.Manifest.fingerprint+"</p><p>Неопределённость и редкие эффекты указаны в деталях. Доход/мин включает 8 секунд меню. Цена материалов — их упущенная продажная стоимость. Взвешенные строки требуют всех пяти типов.</p><input placeholder='Фильтр по названию' oninput='document.querySelectorAll(\"tbody tr\").forEach(r=>r.hidden=!r.textContent.toLowerCase().includes(this.value.toLowerCase()))'><table><thead><tr><th>Набор</th><th>Кирка/стадия/фон/бот</th><th>n</th><th>Глубина</th><th>Стоимость</th><th>Монет/мин</th><th>Δ</th><th>95% Δ</th></tr></thead><tbody>");
            var exportItems = session.Scenarios.Select(Get).ToList();
            if (session.Plan.tiers.All(x=>x))
                foreach (var group in session.Scenarios.GroupBy(s => new { s.point,s.background,s.active })) exportItems.AddRange(View(-1,group.Key.point,group.Key.background,group.Key.active));
            foreach (var item in exportItems)
            {
                var scenario = item.scenario; if (item.Count == 0) continue;
                var ci = Interval(item); var cost = Cost(item); double delta = Delta(item);
                var fields = new List<string> { scenario.id,scenario.tier.ToString(),scenario.point.ToString(),scenario.background.ToString(),scenario.active?"1":"0",scenario.mask.ToString(),item.Count.ToString(),
                    LabFiles.Number(item.Mean(r=>r.depth)),LabFiles.Number(item.Median(r=>r.depth)),LabFiles.Number(item.Min(r=>r.depth)),LabFiles.Number(item.Max(r=>r.depth)),LabFiles.Number(item.Mean(r=>r.seconds)),LabFiles.Number(item.Median(r=>r.seconds)),LabFiles.Number(item.Mean(r=>r.value)),LabFiles.Number(item.Rate),LabFiles.Number(delta),LabFiles.Number(ci.low),LabFiles.Number(ci.high),
                    LabFiles.Number(item.Mean(r=>r.blocks)),LabFiles.Number(item.Mean(r=>r.tools)),LabFiles.Number(item.Mean(r=>r.benches)),LabFiles.Number(item.Mean(r=>r.abilities)),LabFiles.Number(item.Mean(r=>r.censored)),cost.coins<0?"":cost.coins.ToString(),LabFiles.Number(cost.total),!session.Plan.diagnostic && ci.low>0 && delta>0?LabFiles.Number(cost.total/delta):"",LabFiles.Number(Interaction(item)),LabFiles.Number(TripleGain(item)) };
                foreach(var kind in ResourceInventory.Materials){int index=(int)kind;fields.Add(LabFiles.Number(item.Mean(r=>r.resources[index])));}fields.Add(Reliability(item));
                csv.AppendLine(string.Join(",",fields.Select(QuoteCsv)));
                html.Append("<tr><td><details><summary>"+WebUtility.HtmlEncode(scenario.Title)+"</summary><pre>"+WebUtility.HtmlEncode(Detail(item))+"</pre></details></td><td>"+scenario.tier+" / "+scenario.point+" / "+scenario.background+" / "+scenario.active+"</td><td>"+item.Count+"</td><td>"+fields[7]+"</td><td>"+fields[13]+"</td><td>"+fields[14]+"</td><td>"+fields[15]+"</td><td>"+fields[16]+" … "+fields[17]+"</td></tr>");
            }
            html.Append("</tbody></table></html>");
            LabFiles.Atomic(Path.Combine(directory,"summary.csv"),csv.ToString()); LabFiles.Atomic(Path.Combine(directory,"report.html"),html.ToString());
        }
        static string QuoteCsv(string s) => "\""+s.Replace("\"","\"\"")+"\"";
    }
}
#endif
