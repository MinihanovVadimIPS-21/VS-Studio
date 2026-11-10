using ECommerceCsvImport.Models;
using ECommerceCsvImport.Services;
using Microsoft.AspNetCore.Mvc;

namespace ECommerceCsvImport.Controllers;

public class ProductsController : Controller
{
    private readonly IProductRepository _repository;
    private readonly IProductImportService _importService;
    private readonly IWebHostEnvironment _environment;

    public ProductsController(
        IProductRepository repository,
        IProductImportService importService,
        IWebHostEnvironment environment)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _importService = importService ?? throw new ArgumentNullException(nameof(importService));
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));
    }

    [HttpGet]
    public IActionResult Index()
    {
        var vm = new ProductsIndexViewModel { Products = _repository.GetAll() };
        return View(vm);
    }

    [HttpGet]
    public IActionResult Import()
    {
        return View(new ImportViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Import(string filePath)
    {
        var vm = new ImportViewModel { FilePath = filePath ?? string.Empty };
        var resolved = ResolveFilePath(filePath);

        if (string.IsNullOrWhiteSpace(resolved))
        {
            var r = new ImportResult();
            r.Errors.Add(new ImportError { RowNumber = 0, Message = "Файл не найден" });
            vm.Result = r;
            return View(vm);
        }

        vm.Result = _importService.ImportFromCsv(resolved);
        return View(vm);
    }

    private string ResolveFilePath(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return string.Empty;
        filePath = filePath.Trim();

        if (Path.IsPathRooted(filePath)) return filePath;

        var relative = filePath.TrimStart('/', '\\');
        var webRoot = _environment.WebRootPath
                      ?? Path.Combine(_environment.ContentRootPath, "wwwroot");
        return Path.Combine(webRoot, relative.Replace('/', Path.DirectorySeparatorChar));
    }
}