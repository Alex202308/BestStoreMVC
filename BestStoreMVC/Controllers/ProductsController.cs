using BestStoreMVC.Models;
using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Threading.Tasks;

namespace BestStoreMVC.Controllers
{
    public class ProductsController : Controller
    {
        private readonly string _connectionString;
        private readonly IWebHostEnvironment _environment;

        public ProductsController(IConfiguration configuration, IWebHostEnvironment environment)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
            _environment = environment;
        }

        public async Task<IActionResult> Index()
        {
            using (IDbConnection db = new SqlConnection(_connectionString))
            {
                var products = await db.QueryAsync<Product>("SELECT * FROM Products ORDER BY Id DESC");
                return View(products.ToList());
            }
        }

        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Create(ProductDto productDto)
        {
            if (productDto.ImageFile == null)
            {
                ModelState.AddModelError("ImageFile", "Требуется загрузка изображения");
            }

            if (!ModelState.IsValid)
            {
                return View(productDto);
            }

            // Сохранение изображения
            string newFileName = DateTime.Now.ToString("yyyyMMddHHmmssfff") + Path.GetExtension(productDto.ImageFile!.FileName);
            string imageFullPath = Path.Combine(_environment.WebRootPath, "products", newFileName);

            using (var stream = System.IO.File.Create(imageFullPath))
            {
                await productDto.ImageFile.CopyToAsync(stream);
            }

            // Создание нового продукта
            var product = new Product()
            {
                Name = productDto.Name,
                Brand = productDto.Brand,
                Category = productDto.Category,
                Price = productDto.Price,
                Description = productDto.Description,
                ImageFileName = newFileName,
                CreatedAt = DateTime.Now,
            };

            // Вставка данных в базу через Dapper
            using (IDbConnection db = new SqlConnection(_connectionString))
            {
                var sql = "INSERT INTO Products (Name, Brand, Category, Price, Description, ImageFileName, CreatedAt) " +
                          "VALUES (@Name, @Brand, @Category, @Price, @Description, @ImageFileName, @CreatedAt)";
                await db.ExecuteAsync(sql, product);
            }

            return RedirectToAction("Index", "Products");
        }

        public async Task<IActionResult> Edit(int id)
        {
            using (IDbConnection db = new SqlConnection(_connectionString))
            {
                var product = await db.QueryFirstOrDefaultAsync<Product>("SELECT * FROM Products WHERE Id = @Id", new { Id = id });

                if (product == null)
                {
                    return RedirectToAction("index", "Products");
                }

                var productDto = new ProductDto
                {
                    Name = product.Name,
                    Brand = product.Brand,
                    Category = product.Category,
                    Price = product.Price,
                    Description = product.Description
                };

                ViewData["ProductId"] = product.Id;
                ViewData["ImageFileName"] = product.ImageFileName;
                ViewData["CreatedAt"] = product.CreatedAt.ToString("MM/dd/yyyy");

                return View(productDto);
            }
        }

        [HttpPost]
        public async Task<IActionResult> Edit(int id, ProductDto productDto)
        {
            using (IDbConnection db = new SqlConnection(_connectionString))
            {
                var product = await db.QueryFirstOrDefaultAsync<Product>("SELECT * FROM Products WHERE Id = @Id", new { Id = id });

                if (product == null)
                {
                    return RedirectToAction("Index", "Products");
                }

                if (!ModelState.IsValid)
                {
                    ViewData["ProductId"] = product.Id;
                    ViewData["ImageFileName"] = product.ImageFileName;
                    ViewData["CreatedAt"] = product.CreatedAt.ToString("MM/dd/yyyy");
                    return View(productDto);
                }

                // Обновление файла изображения
                string newFileName = product.ImageFileName;
                if (productDto.ImageFile != null)
                {
                    newFileName = DateTime.Now.ToString("yyyyMMddHHmmssfff") + Path.GetExtension(productDto.ImageFile.FileName);
                    string imageFullPath = Path.Combine(_environment.WebRootPath, "products", newFileName);

                    using (var stream = System.IO.File.Create(imageFullPath))
                    {
                        await productDto.ImageFile.CopyToAsync(stream);
                    }

                    // Удаление старого изображения
                    string oldImageFullPath = Path.Combine(_environment.WebRootPath, "products", product.ImageFileName);
                    System.IO.File.Delete(oldImageFullPath);
                }

                product.Name = productDto.Name;
                product.Brand = productDto.Brand;
                product.Category = productDto.Category;
                product.Price = productDto.Price;
                product.Description = productDto.Description;
                product.ImageFileName = newFileName;

                var sql = "UPDATE Products SET Name = @Name, Brand = @Brand, Category = @Category, Price = @Price, " +
                          "Description = @Description, ImageFileName = @ImageFileName WHERE Id = @Id";

                await db.ExecuteAsync(sql, product);
            }

            return RedirectToAction("Index", "Products");
        }

        public async Task<IActionResult> Delete(int id)
        {
            using (IDbConnection db = new SqlConnection(_connectionString))
            {
                var product = await db.QueryFirstOrDefaultAsync<Product>("SELECT * FROM Products WHERE Id = @Id", new { Id = id });
                if (product == null)
                {
                    return RedirectToAction("index", "Products");
                }

                string imageFullPath = Path.Combine(_environment.WebRootPath, "products", product.ImageFileName);
                System.IO.File.Delete(imageFullPath);

                await db.ExecuteAsync("DELETE FROM Products WHERE Id = @Id", new { Id = id });

                return RedirectToAction("index", "Products");
            }
        }
    }
}
