using System;

namespace PickaxeFall
{
    // Serialized into the balance asset, lab manifest and unfinished run. No mutable
    // global configuration: each simulation and resumed run owns its rules.
    [Serializable]
    public sealed class ProgressionBalance
    {
        public ProgressionBalance(){ExpandSlots();}
        public int energyChargeModel = 1;
        public bool worldRecipes=true;
        public void ExpandSlots()
        {
            if(sellPrices.Length==21){Array.Resize(ref sellPrices,ResourceInventory.SlotCount);foreach(var k in ResourceInventory.Materials)if((int)k>=23)sellPrices[(int)k]=WorldCatalog.Price(k);}
            teamMaterials=ExpandRows(teamMaterials);plungeMaterials=ExpandRows(plungeMaterials);
        }
        static int[] ExpandRows(int[] source){if(source.Length!=63)return source;var target=new int[3*ResourceInventory.SlotCount];for(int row=0;row<3;row++)Array.Copy(source,row*21,target,row*ResourceInventory.SlotCount,21);return target;}
        public int[] damageSteps = { 1, 2, 3, 4, 8 }, durabilitySteps = { 3, 6, 9, 14, 32 };
        public float energyCapacity = 60, capacityStep = 8, energyRegen = 3, regenStep = .12f, efficiencyStep = .01f;
        public float dashCost = 45, plungeCost = 45, dashSpeed = 10, dashSpeedStep = .6f;
        public int impactStep = 1, shrapnelStep = 1;
        public long[] sellPrices = { 1, 2, 5, 10, 22, 45, 120, 3, 0, 0, 240, 16, 12, 30, 0, 0, 0, 0, 0, 0, 60 };
        public double[] tierPrices = { 1, 1.4, 2.3, 3.4, 5 };
        public int[] basePrices = { 30, 25, 10, 10, 250, 8, 12, 12, 6, 12, 40, 25, 30, 60, 250, 60, 55 };
        public double[] growth = { 1.28, 1.26, 1.3, 1.35, 1, 1.19, 1.32, 1.32, 1.18, 1.18, 1.28, 1.15, 1.5, 2.5, 1, 1.55, 1.45 };
        public double[] chanceSteps = { 0, 0, .02, .005, 0, .005, .01, .01, .005, .005, 0, 0, 0, 0, 0, 0, 0 };
        public long[] teamCoins = { 250, 1000, 4000 }, plungeCoins = { 250, 1200, 5000 }, unlockCoins = { 200, 350 };
        public int[] unlockStone = { 20, 30 }, unlockOre = { 10, 6 };
        public int[] teamMaterials = { 0, 8, 8, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 20, 10, 8, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 20, 10, 4, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
        public int[] plungeMaterials = { 0, 12, 6, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 30, 12, 8, 4, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 20, 10, 4, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
        public int dirtDamage=8,dirtOther=6,stoneDamage=4,stoneOther=3,stageOre=2,tierOre=1;
        public int[] oreGates={6,11,16,21};
        public float[] recipeMultipliers={1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1};
        public float recipeScale = 1, dirtGrowth = 1.12f, stoneGrowth = 1.18f, oreGrowth = 1.15f, tierOreGrowth = 1.20f;
        public static ProgressionBalance Legacy() => new() { worldRecipes=false,energyChargeModel=0, energyRegen=2,regenStep=.04f,plungeCost=60 };
        public void Validate()
        {
            ExpandSlots();
            if(energyChargeModel<0||energyChargeModel>1)throw new InvalidOperationException("Unknown energy charge model.");
            if(recipeMultipliers?.Length!=17 || teamMaterials?.Length!=3*ResourceInventory.SlotCount || plungeMaterials?.Length!=3*ResourceInventory.SlotCount || oreGates?.Length!=4 || damageSteps?.Length!=5 || durabilitySteps?.Length!=5 || tierPrices?.Length!=5 || basePrices?.Length!=17 || growth?.Length!=17 || chanceSteps?.Length!=17 || sellPrices?.Length!=ResourceInventory.SlotCount || teamCoins?.Length!=3 || plungeCoins?.Length!=3 || unlockCoins?.Length!=2 || unlockStone?.Length!=2 || unlockOre?.Length!=2)throw new InvalidOperationException("Неполная конфигурация прогрессии.");
            foreach(float value in new[] {energyCapacity,capacityStep,energyRegen,regenStep,efficiencyStep,dashCost,plungeCost,dashSpeed,dashSpeedStep,recipeScale,dirtGrowth,stoneGrowth,oreGrowth,tierOreGrowth})if(float.IsNaN(value)||float.IsInfinity(value)||value<0)throw new InvalidOperationException("Некорректная конфигурация прогрессии.");
            if(energyCapacity<=0 || energyRegen<=0 || dashCost<=0 || plungeCost<=0 || efficiencyStep*Progression.MaxLevel(UpgradeKind.EnergyEfficiency)>=1 || recipeScale<=0)throw new InvalidOperationException("Некорректная энергия или рецепт.");
            if(energyChargeModel==1 && 3*Math.Min(dashCost,plungeCost)*(1-efficiencyStep*Progression.MaxLevel(UpgradeKind.EnergyEfficiency))<energyCapacity+capacityStep*5)throw new InvalidOperationException("Three uses must exceed the original capacity levels.");
            for(int i=0;i<5;i++)if(damageSteps[i]<1 || durabilitySteps[i]<1 || tierPrices[i]<=0 || double.IsNaN(tierPrices[i]) || double.IsInfinity(tierPrices[i]))throw new InvalidOperationException("Некорректная прибавка кирки.");
            for(int i=0;i<17;i++)if(basePrices[i]<1 || growth[i]<1 || double.IsNaN(growth[i]) || double.IsInfinity(growth[i]) || chanceSteps[i]<0 || double.IsNaN(chanceSteps[i]) || double.IsInfinity(chanceSteps[i]) || chanceSteps[i]*Progression.MaxLevel((UpgradeKind)i)>1)throw new InvalidOperationException("Некорректная цена или вероятность.");
            foreach(float scale in recipeMultipliers)if(scale<=0||float.IsNaN(scale)||float.IsInfinity(scale))throw new InvalidOperationException("Некорректный множитель рецепта.");
            foreach(int count in teamMaterials)if(count<0)throw new InvalidOperationException("Отрицательный рецепт.");
            foreach(int count in plungeMaterials)if(count<0)throw new InvalidOperationException("Отрицательный рецепт.");
            foreach(long price in sellPrices)if(price<0)throw new InvalidOperationException("Отрицательная цена.");
            foreach(long price in teamCoins)if(price<=0)throw new InvalidOperationException("Некорректная цена отряда.");
            foreach(long price in plungeCoins)if(price<=0)throw new InvalidOperationException("Некорректная цена пробоя.");
        }
    }
}
