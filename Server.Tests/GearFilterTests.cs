using RoguesVRaidersServer.Services;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using Xunit;

namespace RoguesVRaidersServer.Tests;

public class GearFilterTests
{
    static readonly MongoId Class4Helmet = new("507f1f77bcf86cd799439001");
    static readonly MongoId Class4Insert = new("507f1f77bcf86cd799439002");
    static readonly MongoId Class3Helmet = new("507f1f77bcf86cd799439003");
    static readonly MongoId Class3Insert = new("507f1f77bcf86cd799439004");
    static readonly MongoId UnarmoredHat = new("507f1f77bcf86cd799439005");
    static readonly MongoId PlateVest = new("507f1f77bcf86cd799439006");
    static readonly MongoId Plate4 = new("507f1f77bcf86cd799439007");
    static readonly MongoId Plate6 = new("507f1f77bcf86cd799439008");
    static readonly MongoId SelfClassHelmet = new("507f1f77bcf86cd799439009");

    static TemplateItem Item(int? armorClass, IEnumerable<Slot>? slots = null) => new()
    {
        Properties = new TemplateItemProperties { ArmorClass = armorClass, Slots = slots },
    };

    static Slot RequiredSlot(bool required, params MongoId[] options) => NamedSlot("Helmet_top", required, options);

    static Slot NamedSlot(string name, bool required, params MongoId[] options) => new()
    {
        Name = name,
        Required = required,
        Properties = new SlotProperties
        {
            Filters = [new SlotFilter { Filter = [.. options] }],
        },
    };

    static Dictionary<MongoId, TemplateItem> Db(params (MongoId id, TemplateItem item)[] entries) =>
        entries.ToDictionary(e => e.id, e => e.item);

    [Fact]
    public void RequiredClass4InsertResolvesToClass4()
    {
        var db = Db(
            (Class4Helmet, Item(0, [RequiredSlot(true, Class4Insert)])),
            (Class4Insert, Item(4)));

        Assert.Equal(4, GearFilter.ResolveClass(Class4Helmet, id => db.GetValueOrDefault(id)));
    }

    [Fact]
    public void SingleOptionSlotIsTreatedAsBuiltInInsert()
    {
        var db = Db(
            (Class3Helmet, Item(0, [RequiredSlot(false, Class3Insert)])),
            (Class3Insert, Item(3)));

        Assert.Equal(3, GearFilter.ResolveClass(Class3Helmet, id => db.GetValueOrDefault(id)));
    }

    [Fact]
    public void UnarmoredHatWithNoSlotsResolvesToZero()
    {
        var db = Db((UnarmoredHat, Item(0)));

        Assert.Equal(0, GearFilter.ResolveClass(UnarmoredHat, id => db.GetValueOrDefault(id)));
    }

    [Fact]
    public void MultiOptionNonRequiredPlateSlotIsIgnored()
    {
        var db = Db(
            (PlateVest, Item(0, [RequiredSlot(false, Plate4, Plate6)])),
            (Plate4, Item(4)),
            (Plate6, Item(6)));

        Assert.Equal(0, GearFilter.ResolveClass(PlateVest, id => db.GetValueOrDefault(id)));
    }

    [Fact]
    public void ItemOwnArmorClassIsConsidered()
    {
        var db = Db((SelfClassHelmet, Item(5)));

        Assert.Equal(5, GearFilter.ResolveClass(SelfClassHelmet, id => db.GetValueOrDefault(id)));
    }

    [Fact]
    public void UnknownTemplateResolvesToZero()
    {
        var mystery = new MongoId("507f1f77bcf86cd799439099");
        Assert.Equal(0, GearFilter.ResolveClass(mystery, _ => null));
    }

    static Dictionary<MongoId, double> Pool(params MongoId[] ids) =>
        ids.ToDictionary(i => i, _ => 100d);

    [Fact]
    public void FilterPoolDropsBelowMinClassAndKeepsAtOrAbove()
    {
        var db = Db(
            (Class4Helmet, Item(0, [RequiredSlot(true, Class4Insert)])),
            (Class4Insert, Item(4)),
            (Class3Helmet, Item(0, [RequiredSlot(true, Class3Insert)])),
            (Class3Insert, Item(3)));
        var pool = Pool(Class4Helmet, Class3Helmet);

        var result = GearFilter.FilterPool(pool, 4, dropUnarmored: false, keepPlateCarriers: false, id => db.GetValueOrDefault(id));

        Assert.Contains(Class4Helmet, result.Keys);
        Assert.DoesNotContain(Class3Helmet, result.Keys);
    }

    [Fact]
    public void FilterPoolDropsUnarmoredWhenRequested()
    {
        var db = Db(
            (UnarmoredHat, Item(0)),
            (Class4Helmet, Item(0, [RequiredSlot(true, Class4Insert)])),
            (Class4Insert, Item(4)));
        var pool = Pool(UnarmoredHat, Class4Helmet);

        var result = GearFilter.FilterPool(pool, 4, dropUnarmored: true, keepPlateCarriers: false, id => db.GetValueOrDefault(id));

        Assert.DoesNotContain(UnarmoredHat, result.Keys);
        Assert.Contains(Class4Helmet, result.Keys);
    }

    [Fact]
    public void FilterPoolKeepsUnarmoredWhenNotDropping()
    {
        var db = Db((UnarmoredHat, Item(0)));
        var pool = Pool(UnarmoredHat);

        var result = GearFilter.FilterPool(pool, 4, dropUnarmored: false, keepPlateCarriers: false, id => db.GetValueOrDefault(id));

        Assert.Contains(UnarmoredHat, result.Keys);
    }

    [Fact]
    public void FilterPoolLeavesPoolUntouchedWhenAllUnarmoredAndDropRequested()
    {
        var db = Db((UnarmoredHat, Item(0)));
        var pool = Pool(UnarmoredHat);

        var result = GearFilter.FilterPool(pool, 4, dropUnarmored: true, keepPlateCarriers: false, id => db.GetValueOrDefault(id));

        Assert.Same(pool, result);
        Assert.Contains(UnarmoredHat, result.Keys);
    }

    [Fact]
    public void FilterPoolLeavesPoolUntouchedWhenResultWouldBeEmpty()
    {
        var db = Db(
            (Class3Helmet, Item(0, [RequiredSlot(true, Class3Insert)])),
            (Class3Insert, Item(3)));
        var pool = Pool(Class3Helmet);

        var result = GearFilter.FilterPool(pool, 4, dropUnarmored: false, keepPlateCarriers: false, id => db.GetValueOrDefault(id));

        Assert.Same(pool, result);
        Assert.Contains(Class3Helmet, result.Keys);
    }

    static readonly MongoId PlateCarrier = new("507f1f77bcf86cd79943900a");
    static readonly MongoId SoftClass2Vest = new("507f1f77bcf86cd79943900b");
    static readonly MongoId UnarmoredRig = new("507f1f77bcf86cd79943900c");
    static readonly MongoId Class2Insert = new("507f1f77bcf86cd79943900d");

    static Dictionary<MongoId, TemplateItem> VestDb() => Db(
        (PlateCarrier, Item(0, [
            RequiredSlot(true, Class3Insert),
            NamedSlot("front_plate", false, Plate4, Plate6),
        ])),
        (SoftClass2Vest, Item(0, [RequiredSlot(true, Class2Insert)])),
        (UnarmoredRig, Item(0)),
        (Class2Insert, Item(2)),
        (Class3Insert, Item(3)),
        (Plate4, Item(4)),
        (Plate6, Item(6)));

    [Fact]
    public void HasPlateSlotsDetectsPlateSlotByName()
    {
        var db = VestDb();
        Assert.True(GearFilter.HasPlateSlots(PlateCarrier, id => db.GetValueOrDefault(id)));
        Assert.False(GearFilter.HasPlateSlots(SoftClass2Vest, id => db.GetValueOrDefault(id)));
        Assert.False(GearFilter.HasPlateSlots(UnarmoredRig, id => db.GetValueOrDefault(id)));
    }

    [Fact]
    public void FilterPoolKeepsPlateCarrierBelowMinClassWhenRequested()
    {
        var db = VestDb();
        var pool = Pool(PlateCarrier, SoftClass2Vest);

        var result = GearFilter.FilterPool(pool, 4, dropUnarmored: true, keepPlateCarriers: true, id => db.GetValueOrDefault(id));

        Assert.Contains(PlateCarrier, result.Keys);
        Assert.DoesNotContain(SoftClass2Vest, result.Keys);
    }

    [Fact]
    public void FilterPoolKeepsUnarmoredRigAndPlateCarrierInRigMode()
    {
        var db = VestDb();
        var pool = Pool(PlateCarrier, SoftClass2Vest, UnarmoredRig);

        var result = GearFilter.FilterPool(pool, 4, dropUnarmored: false, keepPlateCarriers: true, id => db.GetValueOrDefault(id));

        Assert.Contains(PlateCarrier, result.Keys);
        Assert.Contains(UnarmoredRig, result.Keys);
        Assert.DoesNotContain(SoftClass2Vest, result.Keys);
    }

    [Fact]
    public void FilterPoolDropsPlateCarrierBelowMinClassWhenNotKeeping()
    {
        var db = VestDb();
        var pool = Pool(PlateCarrier, Class4Helmet);
        db[Class4Helmet] = Item(0, [RequiredSlot(true, Class4Insert)]);
        db[Class4Insert] = Item(4);

        var result = GearFilter.FilterPool(pool, 4, dropUnarmored: true, keepPlateCarriers: false, id => db.GetValueOrDefault(id));

        Assert.DoesNotContain(PlateCarrier, result.Keys);
        Assert.Contains(Class4Helmet, result.Keys);
    }
}
