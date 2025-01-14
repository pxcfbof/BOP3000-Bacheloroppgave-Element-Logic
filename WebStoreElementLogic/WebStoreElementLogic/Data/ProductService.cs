using Dapper;
using WebStoreElementLogic.Data;
using WebStoreElementLogic.Interfaces;
using Microsoft.Data.SqlClient;
using System.Data;
using WebStoreElementLogic.Entities;
using System;
using WebStoreElementLogic.Pages;
using System.Text.RegularExpressions;

namespace WebStoreElementLogic.Data
{
    public class ProductService : IProductService
    {
        private readonly IDapperService _dapperService;
        private readonly IConfiguration _configuration;
        private readonly ICustomWebHostEnvironment _customWebHostEnvironment;

        public ProductService(
            IDapperService dapperService, 
            IConfiguration configuration, 
            ICustomWebHostEnvironment customWebHostEnvironment
        )
        {
            try
            {
                _configuration = configuration;
                _dapperService = dapperService;
                _customWebHostEnvironment = customWebHostEnvironment;
            }
            catch (Exception e)
            {
                Console.WriteLine("Could not create DapperService");
                throw;
            }
        }

        public string? ExtractFileName(string fullUrl)
        {
            string pattern = @"\/images\/([\d_A-z]+\.[A-z]+)";
            Regex r = new Regex(pattern, RegexOptions.IgnoreCase);
            Match m = r.Match(fullUrl);

            if (m.Success)
            {
                return m.Groups[1].Value;
            }
            else
            {
                return null;
            }
        }

        public void DeleteImage(int Id)
        {
            // Get url from database
            string url = _dapperService.Get<string>(
                $"SELECT TOP 1 ImageId FROM [Products] WHERE ExtProductId = @ProductId",
                new { ProductId = Id },
                commandType: CommandType.Text
            );

            // Create file path, get filename with regex
            var path = Path.Combine(
                _customWebHostEnvironment.WebRootPath, 
                "images", 
                ExtractFileName(url) ?? "missing"
            );

            // Delete file if it exists
            if (File.Exists(path))
            {
                File.Delete(path);
            }
            else
            {
                Console.WriteLine("File does not exist: " + path);
            }
        }

        public void DeleteImages()
        {
            // Specify the directory path
            string directoryPath = Path.Combine(_customWebHostEnvironment.WebRootPath, "images");

            // Specify the file extensions to delete
            string[] extensions = { ".png", ".jpg", ".jpeg" };

            // Get all files with the specified extensions in the directory
            string[] files = Directory.GetFiles(directoryPath, "*.*")
                                     .Where(file => extensions.Contains(Path.GetExtension(file)))
                                     .ToArray();

            // Delete each file
            foreach (string file in files)
            {
                File.Delete(file);
            }
        }


        public Task<int> Create(Product product)
        {
            int result = 0;
            try
            {
                var dbPara = new DynamicParameters();
                dbPara.Add("@ExtProductId", product.Id, DbType.Int64);
                dbPara.Add("@ProductName", product.Name, DbType.String);
                dbPara.Add("@ProductDesc", product.Descr, DbType.String);
                dbPara.Add("@ImageId", product.URL, DbType.String);
                result = _dapperService.Execute(
                    "[dbo].[spAddProducts]",
                    dbPara,
                    commandType: CommandType.StoredProcedure);
            }
            catch (Exception ex)
            {
                // Log the exception
                Console.WriteLine("Error creating product: " + ex.Message);
                // Re-throw the exception so the calling code can handle it
                throw;
            }

            return Task.FromResult(result);
        }

        public Task<Product> GetByID(int id)
        {
            var product = Task.FromResult(_dapperService.Get<Product>
                ($"SELECT * FROM [Products] WHERE ExtProductId = {id}",
                null, commandType: CommandType.Text));
            return product;
        }

        public Task<int> Delete(int id)
        {
            DeleteImage(id);

            int deleteStock = _dapperService.Execute
                ($"DELETE FROM [Stock] WHERE ExtProductId = {id}",
                null, commandType: CommandType.Text);

            int deleteProduct = _dapperService.Execute
                ($"DELETE FROM [Products] WHERE ExtProductId = {id}",
                null, commandType: CommandType.Text);
            return Task.FromResult(deleteProduct);
        }

        public Task<int> Count(string search)
        {
            var totProduct = Task.FromResult(_dapperService.Get<int>
                ($"SELECT COUNT(*) FROM [Products] WHERE ProductName LIKE '%{search}%'",
                null, commandType: CommandType.Text));
            return totProduct;
        }

        public async Task UpdateQuantity(int productId, decimal quantity)
        {
            try
            {
                _dapperService.Execute
                    ($"UPDATE Stock SET Quantity = Quantity + {quantity} WHERE ExtProductId = {productId}",
                    null, commandType: CommandType.Text);
                Console.WriteLine($"Updated product {productId}'s quantity with {quantity}");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.StackTrace);
            }
        }

        public Task<int> Update(Product product)
        {
            int result = 0;
            try
            {
                var dbPara = new DynamicParameters();
                dbPara.Add("@ExtProductId", product.Id, DbType.Int64);
                dbPara.Add("@ProductName", product.Name, DbType.String);
                dbPara.Add("@ProductDesc", product.Descr, DbType.String);
                dbPara.Add("@ImageId", product.URL, DbType.String);
                result = _dapperService.Execute(
                    "[dbo].[spUpdateProducts]",
                    dbPara,
                    commandType: CommandType.StoredProcedure);
            }
            catch (Exception ex)
            {
                // Log the exception
                Console.WriteLine("Error updating product: " + ex.Message);
                // Re-throw the exception so the calling code can handle it
                throw;
            }

            return Task.FromResult(result);
        }


        public async Task<List<Product>> ListAll(int page, int pageSize, string sortColumnName, string sortDirection, string searchTerm)
        {
            int offset = Math.Max(0, (page - 1) * pageSize); // Ensure the offset is not negative

            // Check if sort column name is provided, if not, set it to "ProductName"
            var sortClause = string.IsNullOrEmpty(sortColumnName) ? "Name" : sortColumnName;

            // Check if sort direction is provided, if not, set it to "ASC"
            sortClause += string.IsNullOrEmpty(sortDirection) ? " DESC" : " " + sortDirection;

            // Prepare the SQL statement
            var sql = $"SELECT * FROM [Products] WHERE ProductName LIKE @searchTerm";

            // Prepare the parameters to pass to the query
            var parameters = new { Offset = offset, PageSize = pageSize, SearchTerm = $"%{searchTerm}%" };

            // Execute the query and retrieve the results as a list of products
            var productsList = _dapperService.GetAll<Product>(sql, parameters, commandType: CommandType.Text);

            return productsList.ToList();
        }


        public Task<List<Product>> ListAllRefresh(string searchTerm, int page, int pageSize, string sortColumnName, string sortDirection)
        {
            int offset = Math.Max(0, (page - 1) * pageSize); // Ensure the offset is not negative

            var sortClause = string.IsNullOrEmpty(sortColumnName) ? "ProductName" : sortColumnName;
            sortClause += string.IsNullOrEmpty(sortDirection) ? " DESC" : " " + sortDirection;

            var sql = $"SELECT * FROM [Products] WHERE ProductName LIKE @searchTerm ORDER BY {sortClause} OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY";

            var parameters = new { Offset = offset, PageSize = pageSize, SearchTerm = "%" + searchTerm + "%" };

            var productsList = _dapperService.GetAll<Product>(sql, parameters, commandType: CommandType.Text);
            return Task.FromResult(productsList.ToList());
        }

        // Task for showing product names from database
        public Task<List<Product>> GetProductNames(string Name)
        {
            var productsList = _dapperService.GetAll<Product>($"SELECT ProductName FROM [Products]", null, commandType: CommandType.Text);
            return Task.FromResult(productsList.ToList());
        }


        public Task<List<Product>> GetProduct(int Id)
        {
            var productsList = _dapperService.GetAll<Product>($"SELECT * FROM [Products] WHERE ExtProductId = {Id}", null, commandType: CommandType.Text);
            return Task.FromResult(productsList.ToList());
        }

        // TODO: endre til � sende id diekte
        public Task<int> GetNextID()
        {
            var product = _dapperService.Get<Product>($"SELECT MAX(ExtProductId + 1) AS Id FROM [Products]", null, commandType: CommandType.Text);
            return Task.FromResult(product.Id);
        }

        public Task<IEnumerable<Product>> GetAll()
        {
            throw new NotImplementedException();
        }

        public Task<bool> doesExistInOrder(int Id)
        {
            var order = _dapperService.Get<Product>(
                $"SELECT TOP 1 ExtOrderId AS Id FROM [Order] WHERE ExtProductId = @ProductId",
                new { ProductId = Id }, 
                commandType: CommandType.Text
            );
            return Task.FromResult(order != null);
        }

        public Task<bool> doesExistInInbound(int Id)
        {
            var inbound = _dapperService.Get<Product>(
                $"SELECT TOP 1 InboundId AS Id FROM [Inbound] WHERE ExtProductId = @ProductId",
                new { ProductId = Id },
                commandType: CommandType.Text
            );
            return Task.FromResult(inbound != null);
        }

        public Task<bool> hasStock(int Id)
        {
            var stock = _dapperService.Get<decimal>(
                $"SELECT TOP 1 Quantity FROM [Stock] WHERE ExtProductId = @ProductId",
                new { ProductId = Id },
                commandType: CommandType.Text
            );
            return Task.FromResult(stock > 0);
        }

        public async Task<List<Product>> GetProducts(string searchTerm, int pageIndex = 1, int pageSize = 10)
        {
            var sql = $"SELECT p.ExtProductId AS Id, p.ProductName AS Name, p.ProductDesc AS Descr, p.ImageId AS URL, CAST(Stock.Quantity AS INT) AS QTY FROM[Products] p LEFT OUTER JOIN[Stock] ON p.ExtProductId = Stock.ExtProductId WHERE p.ProductName LIKE @searchTerm ORDER BY p.ProductName";

            using (var connection = new SqlConnection(_configuration["ConnectionStrings:DefaultConnection"]))
            {
                try
                {
                    await connection.OpenAsync();
                    var products = await connection.QueryAsync<Product>(sql, new { searchTerm = $"%{searchTerm}%", pageIndex = pageIndex, pageSize = pageSize });
                    return products.ToList();
                }
                catch (Exception ex)
                {
                    Console.WriteLine("ProductService, GetProducts: " + ex.Message);
                    return null;
                }
            }
        }
    }
}
