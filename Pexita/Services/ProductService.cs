﻿using Pexita.Data;
using Pexita.Data.Models;
using Pexita.Data.ViewModels;
using Pexita.Services.Interfaces;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Pexita.Services
{
    public class ProductService : IProductService
    {
        private readonly AppDBContext _Context;
        public ProductService(AppDBContext Context)
        {
            _Context = Context;
        }
        public bool AddProduct(ProductVM product)
        {
            try
            {
                ProductModel NewProduct = new()
                {
                    Title = product.Title,
                    Description = product.Description,
                    Price = product.Price,
                    BrandName = product.BrandName,
                    IsPurchasedBefore = product.IsPurchasedBefore,
                    DatePurchased = product.IsPurchasedBefore ? product.DatePurchased!.Value : null,
                    Rate = product.IsPurchasedBefore ? product.Rate!.Value : null,
                    Category = product.Category,
                    ProductPicURL = product.ProductPicURL,
                    DateAdded = DateTime.UtcNow
                };
                _Context.Products.Add(NewProduct);
                _Context.SaveChanges();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public List<ProductModel> GetAllProducts() => _Context.Products.ToList();

        public ProductModel GetProductByID(int id) => _Context.Products.First(n => n.ID == id)!;

        public ProductModel UpdateProductInfo(int id, ProductVM product)
        {
            ProductModel Product = _Context.Products.FirstOrDefault(n => n.ID == id);
            if (Product != null)
            {
                Product.Title = product.Title;
                Product.Description = product.Description;
                Product.Price = product.Price;
                Product.BrandName = product.BrandName;
                Product.IsPurchasedBefore = product.IsPurchasedBefore;
                Product.DatePurchased = product.IsPurchasedBefore ? product.DatePurchased!.Value : null;
                Product.Rate = product.IsPurchasedBefore ? product.Rate!.Value : null;
                Product.Category = product.Category;
                Product.ProductPicURL = product.ProductPicURL;
                _Context.SaveChanges();
            }
            return Product;
        }
        public bool DeleteProduct(int id)
        {
            ProductModel Product =  GetProductByID(id);
            if (Product != null)
            {
                _Context.Products.Remove(Product);
                _Context.SaveChanges();
                return true;
            }
            return false;
        }
    }
}
