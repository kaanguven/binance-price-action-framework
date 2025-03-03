using Project.service;
using Project.service.impl;

var builder = WebApplication.CreateBuilder(args);

// Service katmanını container'a ekle
builder.Services.AddControllers(); // Controller desteğini ekle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpClient();
builder.Services.AddScoped<CryptoService, CryptoServiceImpl>();
builder.Services.AddScoped<ElliottWaveService, ElliottWaveServiceImpl>();


var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization(); // Yetkilendirme middleware'ini ekle

// Controller route'ları için
app.MapControllers();

app.Run();