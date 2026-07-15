using RoguesVRaidersServer.Services;
using SPTarkov.Server.Core.Models.Common;
using Xunit;

namespace RoguesVRaidersServer.Tests;

public class AmmoRankerTests
{
    static readonly MongoId M995 = new("59e690b686f7746c9f75e848");
    static readonly MongoId M855A1 = new("54527ac44bdc2d36668b4567");
    static readonly MongoId M855 = new("54527a984bdc2d4e668b4567");
    static readonly MongoId Fmj = new("59e6920f86f77411d82aa167");
    static readonly MongoId Vog = new("5656eb674bdc2d35148b457c");

    static AmmoRanker.AmmoProps? Props(MongoId id) => id switch
    {
        _ when id == M995 => new AmmoRanker.AmmoProps(53, 42, "bullet"),
        _ when id == M855A1 => new AmmoRanker.AmmoProps(44, 47, "bullet"),
        _ when id == M855 => new AmmoRanker.AmmoProps(31, 54, "bullet"),
        _ when id == Fmj => new AmmoRanker.AmmoProps(23, 56, "bullet"),
        _ when id == Vog => new AmmoRanker.AmmoProps(0, 199, "grenade"),
        _ => null,
    };

    static Dictionary<MongoId, double> Pool(params MongoId[] ids) =>
        ids.ToDictionary(i => i, _ => 1000d);

    [Fact]
    public void BestPenGetsTopWeight()
    {
        var pool = Pool(M995, M855A1, M855, Fmj);
        AmmoRanker.Reweight(pool, Props, [60, 25, 10], 1);
        Assert.Equal(60, pool[M995]);
        Assert.Equal(25, pool[M855A1]);
        Assert.Equal(10, pool[M855]);
        Assert.Equal(1, pool[Fmj]);
    }

    [Fact]
    public void GrenadesAreUntouched()
    {
        var pool = Pool(Vog);
        AmmoRanker.Reweight(pool, Props, [60, 25, 10], 1);
        Assert.Equal(1000d, pool[Vog]);
    }

    [Fact]
    public void UnknownTemplatesAreUntouched()
    {
        var mystery = new MongoId("507f1f77bcf86cd799439011");
        var pool = Pool(M995, mystery);
        AmmoRanker.Reweight(pool, Props, [60], 1);
        Assert.Equal(60, pool[M995]);
        Assert.Equal(1000d, pool[mystery]);
    }

    [Fact]
    public void PenTieBrokenByDamage()
    {
        var a = new MongoId("507f1f77bcf86cd799439012");
        var b = new MongoId("507f1f77bcf86cd799439013");
        var pool = Pool(b, a);
        AmmoRanker.Reweight(pool,
            id => new AmmoRanker.AmmoProps(30, id == a ? 70 : 50, "bullet"),
            [60, 25], 1);
        Assert.Equal(60, pool[a]);
        Assert.Equal(25, pool[b]);
    }
}
