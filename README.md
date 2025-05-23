# Дипломный проект на тему **Численные методы оптимизации для решения инженерных задач**

> Репозиторий состоит из двух независимых частей  
> 1. исследовательский Jupyter‑ноутбук;  
> 2. настольное приложение на **C# / Avalonia**.

---

## Структура проекта

```
.
├── main_notebook/
│   └── Diploma_Full.ipynb      ← полный исследовательский блокнот
└── Desktop/
    └── DiplomApp/              ← GUI‑приложение (.NET 8, Avalonia)
```

| Папка | Назначение |
|-------|------------|
| **main_notebook** | расчёты, визуализация, эксперименты |
| **Desktop/DiplomApp** | готовое приложение с пользовательским интерфейсом |

---

## Быстрый старт

### 1. Запуск ноутбука

```bash
# клонирование репозитория
git clone https://github.com/JlacTo4Ka/Diploma.git

# Запуск в Google Colab файла
Diploma/main_notebook/Diploma_Full.ipynb
```

> **Diploma_Full.ipynb** содержит подробное описание модели,  
> эксперименты с различными методами оптимизации и выводы.

---

### 2. Сборка и запуск **DiplomApp**

> **Требования**: .NET 8 SDK, Windows 10+ / Linux X11 / macOS 11+.

```bash
cd Desktop/DiplomApp

# восстановление пакетов и сборка
dotnet restore
dotnet build -c Release

# запуск
dotnet run -c Release
```

* При первом запуске приложение создаёт виртуальное окружение **python_env**  
  и автоматически устанавливает необходимые Python‑библиотеки.  
* Результаты оптимизации отображаются в виде интерактивной таблицы  
  и 3‑D графиков распределения температуры вдоль трубопровода.

---

## Полезные ссылки

* **Avalonia UI** — <https://avaloniaui.net>  
* **SciPy Optimize** — <https://docs.scipy.org/doc/scipy/reference/optimize.html>  
* **Jupyter** — <https://jupyter.org/>

---

