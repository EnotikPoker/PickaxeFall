#if BALANCE_LAB || UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;

namespace PickaxeFall.BalanceLab
{
    public static class ProfileAuditReport
    {
        public static void Append(StringBuilder html,string directory,LabResult[] results)
        {
            var csv=new StringBuilder("scenario,seed,profileLevel,level2Seconds,level3Seconds,level4Seconds,level5Seconds,dashSeconds,plungeSeconds,depth,value,blocks,profileCoins,mandatorySkillCoins,bridgeStone,bridgeIron,portalObsidian,bridgeSeconds,portalSeconds\n");
            html.Append("<h2>Уровни профиля · шесть часов</h2><p>Учитываются обязательные навыки, накопленная добыча, платные переходы, покупки приёмов и строительство. −1 означает: не достигнуто за время наблюдения. Задержки по разным причинам могут пересекаться.</p>");
            foreach(var row in results)
            {
                csv.AppendLine(string.Join(",",new[]{row.scenario,row.seed.ToString(),row.finalProfileLevel.ToString()}
                    .Concat(row.profileLevelTimes.Skip(1).Select(LabFiles.Number)).Concat(new[]{LabFiles.Number(row.dashOpenedAt),LabFiles.Number(row.plungeOpenedAt),LabFiles.Number(row.depth),LabFiles.Number(row.value),LabFiles.Number(row.blocks),row.profileCoinsSpent.ToString(),row.requiredSkillCoinsSpent.ToString(),row.bridgeStone.ToString(),row.bridgeIron.ToString(),row.portalObsidian.ToString(),LabFiles.Number(row.bridgeCompletedAt),LabFiles.Number(row.portalCompletedAt)})));
            }
            LabFiles.Atomic(Path.Combine(directory,"profile-levels.csv"),csv.ToString());
            var maps=new StringBuilder("scenario,seed,map,runs,seconds,value\n");foreach(var row in results)for(int m=0;m<3;m++)maps.AppendLine(string.Join(",",row.scenario,row.seed,(MapId)m,LabFiles.Number(row.mapRuns[m]),LabFiles.Number(row.mapSeconds[m]),LabFiles.Number(row.mapValues[m])));LabFiles.Atomic(Path.Combine(directory,"profile-maps.csv"),maps.ToString());
            var waits=new StringBuilder("scenario,seed,targetLevel,skillsSeconds,miningSeconds,coinsSeconds\n");
            foreach(var row in results)for(int i=0;i<4;i++)waits.AppendLine(string.Join(",",row.scenario,row.seed,i+2,LabFiles.Number(row.profileSkillsBlocked[i]),LabFiles.Number(row.profileMiningBlocked[i]),LabFiles.Number(row.profileCoinsBlocked[i])));
            LabFiles.Atomic(Path.Combine(directory,"profile-blockers.csv"),waits.ToString());
            html.Append("<table><tr><th>Стратегия / стройка</th><th>Покрытие</th><th>Уровни 2 / 3 / 4 / 5: медиана минут среди достигших</th><th>Достигли ур. 5</th><th>Рывок / пробой, мин</th></tr>");
            foreach(var group in results.GroupBy(r=>r.scenario))
            {
                double Median(double[] v)=>v.Length==0?-1:(v.OrderBy(x=>x).ElementAt((v.Length-1)/2)+v.OrderBy(x=>x).ElementAt(v.Length/2))/2;
                string times=string.Join(" / ",Enumerable.Range(1,4).Select(i=>LabFiles.Number(Median(group.Where(r=>r.profileLevelTimes[i]>=0).Select(r=>r.profileLevelTimes[i]/60).ToArray()))));
                html.Append("<tr><td>"+group.Key+"</td><td>"+group.Count()+"</td><td>"+times+"</td><td>"+group.Count(r=>r.finalProfileLevel==5)+" / "+group.Count()+"</td><td>"+LabFiles.Number(Median(group.Where(r=>r.dashOpenedAt>=0).Select(r=>r.dashOpenedAt/60).ToArray()))+" / "+LabFiles.Number(Median(group.Where(r=>r.plungeOpenedAt>=0).Select(r=>r.plungeOpenedAt/60).ToArray()))+"</td></tr>");
            }
            html.Append("</table>");
        }
    }
}
#endif
