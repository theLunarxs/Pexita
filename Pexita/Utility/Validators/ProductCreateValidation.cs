﻿using FluentValidation;
using Microsoft.IdentityModel.Tokens;
using Pexita.Data.Entities.Products;
using Pexita.Services;
using Pexita.Services.Interfaces;

namespace Pexita.Utility.Validators
{
    public class ProductCreateValidation : AbstractValidator<ProductCreateDTO>
    {
        private readonly IBrandService _brandService;
        private readonly ITagsService _tagsService;
        private readonly IPexitaTools _pexitaTools;
        private readonly IProductService _productService;
        public ProductCreateValidation(IBrandService BrandService,
            ITagsService tagsService, IPexitaTools pexitaTools,
            IProductService productService)
        {
            _brandService = BrandService;
            _tagsService = tagsService;
            _pexitaTools = pexitaTools;
            _productService = productService;

            RuleFor(x => x.Title).NotEmpty().MaximumLength(50)
                .WithMessage("Title {PropertyName} Cannot be Empty Or More Than 30 Characters");

            RuleFor(x => x.Title).Must((product, title) =>
            {
                return !_productService.IsProductAlready(product.Brand, title);
            }).WithMessage("Product already exists for this brand with title : {PropertyName}");

            RuleFor(x => x.Quantity).NotEmpty().GreaterThanOrEqualTo(0)
                .When(x => x.IsAvailable);

            RuleFor(x => x.Price).NotEmpty().GreaterThanOrEqualTo(0)
                .When(x => x.IsAvailable);

            RuleFor(x => x.Brand).NotEmpty()
                .Must(_brandService.IsBrand);

            RuleFor(x => x.Tags).Must(_tagsService.IsTag)
                .When(x => !x.Tags.IsNullOrEmpty());

            RuleFor(x => x.Colors).NotEmpty();

            RuleForEach(x => x.ProductPics).NotEmpty().Must(file => _pexitaTools.PictureFileValidation(file, 10));

            RuleFor(x => x.Price).GreaterThanOrEqualTo(0);
        }
    }
    public class ProductUpdateRateValidation : AbstractValidator<UpdateProductRateDTO>
    {
        public ProductUpdateRateValidation()
        {
            RuleFor(x => x.ProductID).NotEmpty().NotNull();

            RuleFor(x => x.ProductRating)
                .GreaterThanOrEqualTo(0).WithErrorCode("400").WithMessage("Rate cannot be less than zeroes")
                .LessThanOrEqualTo(5).WithErrorCode("400").WithMessage("Rate cannot be more than 5");
        }
    }
    public class ProductUpdateValidation : AbstractValidator<ProductUpdateDTO>
    {
        private readonly IBrandService _brandService;
        private readonly ITagsService _tagsService;
        private readonly IPexitaTools _pexitaTools;

        public ProductUpdateValidation(IBrandService brandService,
            ITagsService tagsService, IPexitaTools pexitaTools)
        {
            _brandService = brandService;
            _tagsService = tagsService;
            _pexitaTools = pexitaTools;

            RuleFor(x => x.Title).NotEmpty().MaximumLength(50)
                .WithMessage("Title {PropertyName} Cannot be Empty Or More Than 30 Characters");

            RuleFor(x => x.Quantity).NotEmpty().GreaterThanOrEqualTo(0)
                .When(x => x.IsAvailable);

            RuleFor(x => x.Price).NotEmpty().GreaterThanOrEqualTo(0)
                .When(x => x.IsAvailable);

            RuleFor(x => x.Brand).NotEmpty()
                .Must(_brandService.IsBrand);

            RuleFor(x => x.Tags).Must(_tagsService.IsTag)
                .When(x => !x.Tags.IsNullOrEmpty());

            RuleFor(x => x.Colors).NotEmpty();

            RuleForEach(x => x.ProductPics).NotEmpty()
                .Must(file => _pexitaTools.PictureFileValidation(file, 10));

            RuleFor(x => x.Price).GreaterThanOrEqualTo(0);
        }

    }
    public class ProductCommentValidation : AbstractValidator<ProductCommentDTO>
    {
        private readonly IProductService _productService;
        private readonly IUserService userService;

        public ProductCommentValidation(IProductService productService, IUserService userService)
        {
            this.userService = userService;
            _productService = productService;

            RuleFor(x => x.ProductID).NotEmpty().NotNull();

            RuleFor(x => x.Comment).NotEmpty().NotNull();

            RuleFor(x => x.Comment.ProductID).Must(x => productService.GetProductByID(x) != null);
            RuleFor(x => x.Comment.Product).NotNull().NotEmpty();


            RuleFor(x => x.Comment.UserID).NotEmpty().Must(x => userService!.GetUserByID(x!.Value) != null);
            RuleFor(x => x.Comment.User).NotEmpty().NotNull();

            RuleFor(x => x.Comment.Text).NotEmpty().NotNull();
            RuleFor(x => x.Comment.DateCreated).NotEmpty().NotNull();
        }
    }
}
