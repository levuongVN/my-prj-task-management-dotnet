using NSubstitute;
using TaskFlow.Application.DTOs.Labels;
using TaskFlow.Application.Interfaces;
using TaskFlow.Application.Services;
using TaskFlow.Domain.Entities;

namespace TaskFlow.Tests.Labels;

public class LabelServiceTests
{
    private readonly ILabelRepository _labels = Substitute.For<ILabelRepository>();

    private LabelService Sut() => new(_labels);

    private static readonly Guid UserId = Guid.NewGuid();

    [Fact]
    public async Task GetAll_MapDanhSach()
    {
        _labels.GetByUserAsync(UserId).Returns(
        [
            new Label { Name = "a", Color = "#111111", UserId = UserId },
            new Label { Name = "b", Color = "#222222", UserId = UserId },
        ]);

        var res = await Sut().GetAllAsync(UserId);

        Assert.Equal(2, res.Count);
        Assert.Equal("#111111", res[0].Color);
    }

    [Fact]
    public async Task Create_NameTrong_NemArgument()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => Sut().CreateAsync(UserId, new LabelRequest { Name = "  " }));

        Assert.Equal("Label name is required", ex.Message);
    }

    [Fact]
    public async Task Create_NameQua50_NemArgument()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => Sut().CreateAsync(UserId, new LabelRequest { Name = new string('n', 51) }));

        Assert.Equal("Label name must be 50 characters or fewer", ex.Message);
    }

    [Fact]
    public async Task Create_NameTrung_NemArgument()
    {
        _labels.NameExistsAsync(UserId, "work").Returns(true);

        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => Sut().CreateAsync(UserId, new LabelRequest { Name = "work" }));

        Assert.Equal("Label name already exists", ex.Message);
    }

    [Fact]
    public async Task Create_HopLe_NameTrimVaColorMacDinh()
    {
        _labels.NameExistsAsync(UserId, "work").Returns(false);

        var res = await Sut().CreateAsync(UserId, new LabelRequest { Name = "  work  " });

        Assert.Equal("work", res.Name);
        Assert.Equal("#2563eb", res.Color);
        await _labels.Received(1).AddAsync(Arg.Is<Label>(l => l.UserId == UserId));
        await _labels.Received(1).SaveChangesAsync();
    }

    [Fact]
    public async Task Create_ColorLowercase_DuocChuanHoa()
    {
        _labels.NameExistsAsync(UserId, "x").Returns(false);

        var res = await Sut().CreateAsync(UserId, new LabelRequest { Name = "x", Color = "  #ff00aa " });

        Assert.Equal("#FF00AA", res.Color);
    }

    [Fact]
    public async Task Create_ColorSaiDinhDang_NemArgument()
    {
        _labels.NameExistsAsync(UserId, "x").Returns(false);

        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => Sut().CreateAsync(UserId, new LabelRequest { Name = "x", Color = "red" }));

        Assert.Equal("Color must be a hex value like #RRGGBB", ex.Message);
    }

    [Fact]
    public async Task Create_ColorSaiDoDai_NemArgument()
    {
        _labels.NameExistsAsync(UserId, "x").Returns(false);

        await Assert.ThrowsAsync<ArgumentException>(
            () => Sut().CreateAsync(UserId, new LabelRequest { Name = "x", Color = "#FFF" }));
    }

    [Fact]
    public async Task Update_KhongTonTai_NemKeyNotFound()
    {
        _labels.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<Guid>()).Returns((Label?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => Sut().UpdateAsync(Guid.NewGuid(), UserId, new LabelRequest { Name = "x" }));
    }

    [Fact]
    public async Task Update_NameTrungVoiLabelKhac_NemArgument()
    {
        var label = new Label { Name = "old", UserId = UserId };
        _labels.GetByIdAsync(label.Id, UserId).Returns(label);
        _labels.NameExistsAsync(UserId, "dup", Arg.Any<Guid?>()).Returns(true);

        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => Sut().UpdateAsync(label.Id, UserId, new LabelRequest { Name = "dup" }));

        Assert.Equal("Label name already exists", ex.Message);
        Assert.Equal("old", label.Name);
    }

    [Fact]
    public async Task Update_HopLe_DoiNameVaColor()
    {
        var label = new Label { Name = "old", Color = "#000000", UserId = UserId };
        _labels.GetByIdAsync(label.Id, UserId).Returns(label);
        _labels.NameExistsAsync(UserId, "new", label.Id).Returns(false);

        var res = await Sut().UpdateAsync(label.Id, UserId, new LabelRequest { Name = "new", Color = "#abcdef" });

        Assert.Equal("new", res.Name);
        Assert.Equal("#ABCDEF", res.Color);
        _labels.Received(1).Update(label);
        await _labels.Received(1).SaveChangesAsync();
    }

    [Fact]
    public async Task Delete_KhongTonTai_NemKeyNotFound()
    {
        _labels.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<Guid>()).Returns((Label?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => Sut().DeleteAsync(Guid.NewGuid(), UserId));
    }

    [Fact]
    public async Task Delete_HopLe_HardDelete()
    {
        var label = new Label { Name = "gone", UserId = UserId };
        _labels.GetByIdAsync(label.Id, UserId).Returns(label);

        await Sut().DeleteAsync(label.Id, UserId);

        _labels.Received(1).Delete(label);
        await _labels.Received(1).SaveChangesAsync();
    }
}
