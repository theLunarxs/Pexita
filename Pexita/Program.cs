using Microsoft.EntityFrameworkCore;
using Pexita.Data;
using Pexita.Services.Interfaces;
using Pexita.Services;
using AutoMapper;
using Pexita.Additionals;
using Pexita.Utility;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();


builder.Services.AddAutoMapper(typeof(AutoMapperConfig));

builder.Services.AddDbContext<AppDBContext>
    (options => options.UseSqlServer(builder.Configuration.GetConnectionString("DbOne")));

builder.Services.AddTransient<IProductService, ProductService>();
builder.Services.AddTransient<IBrandService, BrandService>();
builder.Services.AddTransient<ITagsService, TagsService>();
builder.Services.AddTransient<IUserService, UserService>();
builder.Services.AddTransient<PexitaTools>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();
InitialData.Seed(app);
app.Run();
