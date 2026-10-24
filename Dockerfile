# === المرحلة 1: البناء (SDK كامل، Image أكبر) ===
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# بننسخ ملفات الـ csproj الأول بس (مش كل الكود) — عشان Docker يقدر يـ"cache"
# خطوة الـ restore، فلو غيرت سطر كود واحد بس (من غير ما تضيف NuGet package جديد)،
# الـ Build الجاي هيبقى أسرع بكتير لأنه مش هيعيد الـ restore من الصفر
COPY ["src/AgFlow.API/AgFlow.API.csproj", "src/AgFlow.API/"]
COPY ["src/AgFlow.Application/AgFlow.Application.csproj", "src/AgFlow.Application/"]
COPY ["src/AgFlow.Infrastructure/AgFlow.Infrastructure.csproj", "src/AgFlow.Infrastructure/"]
COPY ["src/AgFlow.Domain/AgFlow.Domain.csproj", "src/AgFlow.Domain/"]
RUN dotnet restore "src/AgFlow.API/AgFlow.API.csproj"

# دلوقتي بس ننسخ باقي الكود كله، ونبني
COPY . .
WORKDIR "/src/src/AgFlow.API"
RUN dotnet publish "AgFlow.API.csproj" -c Release -o /app/publish --no-restore

# === المرحلة 2: التشغيل الفعلي (Runtime بس، Image أصغر بكتير) ===
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# بنعمل مستخدم غير Root لتشغيل التطبيق — ممارسة أمنية أساسية
# (لو حد اخترق الـ Container، مايبقاش ليه صلاحيات Root كاملة عليه)
RUN adduser --disabled-password --gecos "" appuser
USER appuser

# بننسخ بس الناتج النهائي من مرحلة الـ Build، من غير أي أدوات SDK زيادة
COPY --from=build /app/publish .

EXPOSE 8080
ENTRYPOINT ["dotnet", "AgFlow.API.dll"]