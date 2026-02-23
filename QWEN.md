# ArchiveTransparencQWEN — Контекст проекта

## 📋 Обзор проекта

**Archive Transparency** — фоновая WPF-утилита для Windows, которая показывает содержимое архивов (.zip, .rar, .7z и др.) во всплывающем окне при наведении курсора в Проводнике Windows — без необходимости распаковки.

### Основные возможности

- 📁 Просмотр содержимого архивов без распаковки
- 🖱️ Автоматическое появление при наведении курсора
- 🎨 Тёмный интерфейс в стиле Dracula
- ⚡ Кэширование результатов (настраиваемое)
- 📌 Иконка в системном трее с меню
- 🔕 Не перехватывает фокус (non-activatable окно)
- 📊 Статистика использования
- ⌨️ Горячие клавиши
- 📁 Группировка по папкам (TreeView)
- 🖼️ Предпросмотр изображений

---

## 🏗️ Архитектура

```
┌─────────────────────────────────────────────────────────┐
│                      App.xaml.cs                        │
│  • Dependency Injection контейнер                       │
│  • NotifyIcon (трей) + контекстное меню                 │
│  • HotkeyService, StatisticsService                     │
└─────────────────────────────────────────────────────────┘
                          │
                          ▼
┌─────────────────────────────────────────────────────────┐
│                   ExplorerMonitor                       │
│  • Polling курсора (настраиваемый)                      │
│  • Debouncing для производительности                    │
│  • UI Automation для определения элемента               │
│  • Shell COM для получения пути папки                   │
└─────────────────────────────────────────────────────────┘
                          │
                          ▼
┌─────────────────────────────────────────────────────────┐
│                    ArchiveReader                        │
│  • SharpCompress (нативная поддержка)                   │
│  • 7-Zip CLI (.rar, .7z, .tar, ...)                     │
│  • ZipFile (.zip нативно)                               │
│  • Кэширование с настраиваемым временем                 │
│  • Проверка доступности файла                           │
└─────────────────────────────────────────────────────────┘
                          │
                          ▼
┌─────────────────────────────────────────────────────────┐
│                    TooltipWindow                        │
│  • WPF окно (non-activatable)                           │
│  • Fade-in/fade-out анимация                            │
│  • ItemsControl / TreeView (по настройке)               │
│  • ContextMenu (открыть, копировать, извлечь)           │
│  • Индикатор прогресса                                  │
│  • DPI-aware позиционирование                           │
└─────────────────────────────────────────────────────────┘
```

---

## 📁 Структура проекта

```
ArchiveTransparencQWEN/
├── ArchiveTransparency.csproj    # Проект .NET 8 WPF
├── appsettings.json              # Конфигурация
├── App.xaml / App.xaml.cs        # Точка входа, DI, трей-иконка
├── Config/
│   ├── Settings.cs               # strongly-typed настройки
│   └── ConfigLoader.cs           # загрузчик конфигурации
├── DependencyInjection/
│   └── ServiceContainer.cs       # DI контейнер
├── Models/
│   └── ArchiveEntry.cs           # Модель элемента архива
├── Services/
│   ├── ArchiveReader.cs          # Чтение архивов (SharpCompress + 7-Zip)
│   ├── ExplorerMonitor.cs        # Мониторинг Проводника
│   ├── StatisticsService.cs      # Статистика использования
│   └── HotkeyService.cs          # Глобальные горячие клавиши
├── Windows/
│   ├── TooltipWindow.xaml        # UI всплывающего окна
│   └── TooltipWindow.xaml.cs     # Логика окна
├── Helpers/
│   ├── NativeMethods.cs          # P/Invoke (Win32 API)
│   └── AutoStartHelper.cs        # Автозапуск через реестр
├── Tests/
│   ├── ArchiveTransparency.Tests.csproj
│   ├── ArchiveEntryTests.cs
│   ├── ArchiveReaderTests.cs
│   ├── ConfigTests.cs
│   ├── HotkeyServiceTests.cs
│   └── StatisticsServiceTests.cs
├── README.md                     # Документация пользователя
└── QWEN.md                       # Контекст для ИИ-ассистента
```

---

## 🛠️ Сборка и запуск

### Требования

- Windows 10/11
- .NET 8 SDK (для сборки)
- 7-Zip (опционально, для .rar/.7z)

### Команды

```bash
# Восстановление пакетов
dotnet restore

# Сборка (Debug)
dotnet build

# Сборка (Release)
dotnet build -c Release

# Запуск
dotnet run

# Запуск тестов
dotnet test Tests/ArchiveTransparency.Tests.csproj

# Публикация single-file exe
dotnet publish -c Release -r win-x64 --self-contained
```

### Конфигурация публикации

Проект настроен на публикацию как самодостаточное приложение:

```xml
<PublishSingleFile>true</PublishSingleFile>
<SelfContained>true</SelfContained>
<RuntimeIdentifier>win-x64</RuntimeIdentifier>
```

---

## 📦 Поддерживаемые форматы

| Формат | Расширения | Метод чтения |
|--------|------------|--------------|
| ZIP | `.zip` | `SharpCompress` / `ZipFile` |
| 7-Zip | `.7z` | `SharpCompress` / 7-Zip CLI |
| RAR | `.rar` | `SharpCompress` / 7-Zip CLI |
| TAR | `.tar` | `SharpCompress` / 7-Zip CLI |
| GZIP | `.gz`, `.tgz` | `SharpCompress` / 7-Zip CLI |
| BZip2 | `.bz2`, `.tbz2` | `SharpCompress` / 7-Zip CLI |
| XZ | `.xz`, `.txz` | `SharpCompress` / 7-Zip CLI |
| Другие | `.cab`, `.iso`, `.wim`, `.lzh`, `.lzma`, `.arj` | `SharpCompress` / 7-Zip CLI |

---

## ⚙️ Конфигурация (appsettings.json)

```json
{
  "SevenZipPath": "",                    // Путь к 7z.exe (пусто = автопоиск)
  "CacheExpirationMinutes": 5,           // Время жизни кэша
  "MaxEntries": 200,                     // Макс. элементов для чтения
  "MaxDisplayEntries": 200,              // Макс. элементов для отображения
  "PollingIntervalMs": 300,              // Интервал опроса курсора
  "DebounceIntervalMs": 150,             // Debouncing для производительности
  "TooltipFadeDurationMs": 150,          // Длительность анимации
  "HotkeyShow": "Ctrl+Alt+A",            // Горячая клавиша
  "EnableImagePreview": true,            // Предпросмотр изображений
  "EnableTreeView": false,               // Группировка по папкам
  "ShowStatistics": true,                // Показывать статистику в трее
  "LogFilePath": "logs\\app.log",        // Путь к файлу логов
  "LogLevel": "Information"              // Уровень логирования
}
```

---

## 🔑 Ключевые технические детали

### NuGet пакеты

- `Microsoft.Extensions.DependencyInjection` — DI контейнер
- `Microsoft.Extensions.Configuration.*` — конфигурация
- `Serilog.*` — логирование
- `SharpCompress` — нативное чтение архивов

### NativeMethods.cs (Win32 API)

- `GetCursorPos` — получение позиции курсора
- `WindowFromPoint` / `GetAncestor` — определение окна под курсором
- `GetWindowThreadProcessId` — проверка процесса (explorer.exe)
- `GetWindowLong` / `SetWindowLong` — установка стилей окна
- `RegisterHotKey` / `UnregisterHotKey` — глобальные горячие клавиши

### Стили окна (non-activatable)

```csharp
exStyle |= WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW;
```

Это предотвращает перехват фокуса и появление окна в Alt+Tab.

### Автозапуск

Регистрация через реестр:
```
HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Run
  → ArchiveTransparency = "C:\path\to\ArchiveTransparency.exe"
```

### Логирование

Логи записываются в файл с ежедневной ротацией (7 дней хранения):
```
logs/app-YYYYMMDD.log
```

---

## 🧪 Практики разработки

### Код-стайл

- **Nullable reference types**: включены (`<Nullable>enable</Nullable>`)
- **Implicit usings**: включены (`<ImplicitUsings>enable</ImplicitUsings>`)
- **Именование**: PascalCase для классов/метод, camelCase для полей
- **Асинхронность**: `async/await` для I/O операций

### Тестирование

- **xUnit** для unit-тестов
- **47 тестов** покрывают основные компоненты:
  - `ArchiveEntryTests` — модель элемента архива
  - `ArchiveReaderTests` — парсинг вывода 7-Zip
  - `StatisticsServiceTests` — сервис статистики
  - `HotkeyServiceTests` — парсинг горячих клавиш
  - `ConfigTests` — конфигурация

### Обработка ошибок

- Логирование через Serilog вместо игнорирования
- Проверка доступности файла перед чтением
- Debouncing для предотвращения гонки при быстром перемещении мыши

---

## 📝 Примечания для ИИ-ассистента

- При модификации кода сохраняйте существующий стиль (именование, структура)
- Используйте DI для новых сервисов через `ServiceContainer`
- Для UI изменений используйте существующие ресурсы (цвета, стили из App.xaml)
- Учитывайте DPI-aware позиционирование при работе с координатами
- Сохраняйте non-activatable поведение окна (не удаляйте `WS_EX_NOACTIVATE`)
- Новые настройки добавляйте в `appsettings.json` и `Settings.cs`
- Логируйте важные события через `_logger`
