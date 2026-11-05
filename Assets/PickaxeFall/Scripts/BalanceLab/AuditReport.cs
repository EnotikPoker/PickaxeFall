#if BALANCE_LAB || UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;

namespace PickaxeFall.BalanceLab
{
    public static class AuditReport
    {
        public static void Append(LabSession session,StringBuilder html,string directory,LabResult[] results)
        {
            html.Append("<h2>Покрытие и ремонт</h2><p>"+WebUtility.HtmlEncode(session.Status)+". Отсутствующие задания не считаются нулевыми результатами. Квантили учитывают веса физических веток отряда и спасения.</p>");
            if(session.Plan.mode==4)
            foreach(var group in session.Scenarios.GroupBy(s=>s.stage).OrderBy(g=>g.Key))
            {
                long wanted=group.Sum(s=>(long)s.sampleCount);var ids=group.Select(s=>s.id).ToHashSet();int done=results.Count(r=>ids.Contains(r.scenario));
                html.Append("<p>"+new[] {"Основа кирок","Независимые карты","Час развития","Навыки","Сочетания и максимум"}[group.Key]+": "+done+" / "+wanted+(done==wanted?" — завершено":" — неполное покрытие")+"</p>");
            }
            var visits=new StringBuilder("scenario,seed,tool,depth,seconds,upgrade,tierBefore,tierAfter,durabilityBefore,maximumBefore,restored,nextReached,nextDistance,nextSeconds,weight,terminalCensored\n");
            foreach(var row in results)foreach(var b in row.benchVisits??Array.Empty<BenchVisit>())
                visits.AppendLine(string.Join(",",row.scenario,row.seed,b.tool,b.depth,LabFiles.Number(b.seconds),b.upgrading?1:0,b.tierBefore,b.tierAfter,b.durabilityBefore,b.maximumBefore,b.restored,b.nextReached?1:0,LabFiles.Number(b.nextDistance),LabFiles.Number(b.nextSeconds),LabFiles.Number(b.weight),b.terminalCensored?1:0));
            LabFiles.Atomic(Path.Combine(directory,"benches.csv"),visits.ToString());
            html.Append("<p>Вероятность следующего ремонта ниже считается по визитам, с весами веток. Последние визиты живой кирки в ограниченном забеге исключены. Разные сценарии показаны отдельно.</p><table><tr><th>Сценарий / глубина</th><th>Вес визитов</th><th>Следующий ремонт</th><th>Прочность до</th><th>Восстановлено</th><th>Расстояние</th></tr>");
            int[] depths={0,30,75,125,200,300,int.MaxValue};
            foreach(var group in results.GroupBy(r=>r.scenario))
            for(int band=0;band<depths.Length-1;band++)
            {
                var list=group.SelectMany(r=>r.benchVisits??Array.Empty<BenchVisit>()).Where(b=>b.depth>=depths[band]&&b.depth<depths[band+1]&&!b.terminalCensored).ToArray();double weight=list.Sum(b=>b.weight);if(weight<=0)continue;
                double reached=list.Where(b=>b.nextReached).Sum(b=>b.weight);
                html.Append("<tr><td>"+group.Key+" / "+depths[band]+"–"+(depths[band+1]==int.MaxValue?"∞":depths[band+1].ToString())+"</td><td>"+LabFiles.Number(weight)+"</td><td>"+LabFiles.Number(100*reached/weight)+"%</td><td>"+LabFiles.Number(list.Sum(b=>b.weight*b.durabilityBefore)/weight)+"</td><td>"+LabFiles.Number(list.Sum(b=>b.weight*b.restored)/weight)+"</td><td>"+(reached>0?LabFiles.Number(list.Where(b=>b.nextReached).Sum(b=>b.weight*b.nextDistance)/reached):"—")+"</td></tr>");
            }
            html.Append("</table><h2>История покупок</h2><p>Последний незавершённый спуск измеряется на границе выбранного периода; он не продаётся и не даёт покупки после отсечки. Его censored=1 обозначает границу наблюдения, а не зависание.</p>");
            var purchases=new StringBuilder("scenario,seed,seconds,key,level,title,coins,interval\n");
            foreach(var row in results.Where(r=>r.purchases!=null))
            {
                double last=0;foreach(var purchase in row.purchases){purchases.AppendLine(string.Join(",",row.scenario,row.seed,LabFiles.Number(purchase.seconds),purchase.key,purchase.level,purchase.title,purchase.coins,LabFiles.Number(purchase.seconds-last)));last=purchase.seconds;}
                html.Append("<p>"+row.scenario+" / "+row.seed+": первая покупка "+LabFiles.Number(row.firstPurchase)+" с; покупок "+row.purchases.Length+"; рекорд "+LabFiles.Number(row.depth)+" м; открытие рывка/пробоя "+LabFiles.Number(row.dashOpenedAt)+" / "+LabFiles.Number(row.plungeOpenedAt)+" с.</p>");
            }
            LabFiles.Atomic(Path.Combine(directory,"purchases.csv"),purchases.ToString());
        }
    }
}
#endif
