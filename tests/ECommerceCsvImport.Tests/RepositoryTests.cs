using ECommerceCsvImport.Models;
using ECommerceCsvImport.Services;
using Xunit;

namespace ECommerceCsvImport.Tests;

public class RepositoryTests
{
    [Fact]
    public void Add_AddsProduct_GetAllContainsIt()
    {
        var repo = new InMemoryProductRepository();
        var product = new Product { Sku = "1001", Name = "Ноутбук", Price = 45990m, Brand = "Lenovo" };
        repo.Add(product);
        var all = repo.GetAll();
        Assert.Single(all);
        Assert.Equal("1001", all[0].Sku);
        Assert.Equal(1, repo.Count);
    }

    [Fact]
    public void GetBySku_ReturnsProduct()
    {
        var repo = new InMemoryProductRepository();
        repo.Add(new Product { Sku = "1001", Name = "Ноутбук" });
        var found = repo.GetBySku("1001");
        Assert.NotNull(found);
        Assert.Equal("1001", found!.Sku);
    }

    [Fact]
    public void GetBySku_CaseInsensitive_AndTrimmed()
    {
        var repo = new InMemoryProductRepository();
        repo.Add(new Product { Sku = "1001", Name = "Ноутбук" });
        Assert.NotNull(repo.GetBySku(" 1001 "));
    }

    [Fact]
    public void EmptyRepository_BehavesAsExpected()
    {
        var repo = new InMemoryProductRepository();
        Assert.Equal(0, repo.Count);
        Assert.Empty(repo.GetAll());
        Assert.Null(repo.GetBySku("1001"));
    }
}