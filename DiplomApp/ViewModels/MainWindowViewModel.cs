using System.Collections.ObjectModel;
using DiplomApp.ViewModels;
using ReactiveUI;
using System.IO;
using System.Reactive;
using System.Collections.Generic;
using System;
using Avalonia.Controls;
using Avalonia.Threading;
using System.Linq;
using System.Reactive.Threading.Tasks;
using System.Threading.Tasks;
using Avalonia.Controls.ApplicationLifetimes;
using System.Reactive.Disposables;
using Avalonia.Platform.Storage;
using Avalonia;
using Avalonia.Media;
using DynamicData;
using Avalonia.Media.Imaging;
using System.Net.Http;
using System.ComponentModel;
using System.Net;
using System.Text;
using Tmds.DBus.Protocol;
using System.Reflection;
using DiplomApp.Views;
using Avalonia.Data.Converters;
using System.Diagnostics;
using System.Text.Json;
using System.Globalization;
using System.Text.Json.Serialization;

namespace DiplomApp.ViewModels
{
    public partial class MainWindowViewModel : ViewModelBase
    {
        private SolverConfig _сonfig = new();
        public SolverConfig Config
        {
            get => _сonfig;
            set => this.RaiseAndSetIfChanged(ref _сonfig, value);
        }

        private Bitmap? _resultBitmap;
        public Bitmap? ResultBitmap
        {
            get => _resultBitmap;
            set => this.RaiseAndSetIfChanged(ref _resultBitmap, value);
        }

        private MainWindow _window;
        public MainWindow? MainWindow
        {
            get => _window;
            set => this.RaiseAndSetIfChanged(ref _window, value);
        }

        bool isDarkTheme = true;
        private void SwitchTheme()
        {
            if (isDarkTheme)
                ChangeTheme("avares://DiplomApp/Themes/Dark.axaml");
            else
                ChangeTheme("avares://DiplomApp/Themes/Light.axaml");
        }

        public void ChangeTheme(string newThemePath)
        {
            if (Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                MainWindow.LoadStyles(newThemePath);
            }
        }

        public MainWindowViewModel()
        {

        }
        public MainWindowViewModel(MainWindow win)
        {
            MainWindow = win;
            SwitchTheme();
        }

        private bool _isBusy;                 // ← флаг «идёт расчёт»
        public bool IsBusy
        {
            get => _isBusy;
            set => this.RaiseAndSetIfChanged(ref _isBusy, value);
        }

        public async void PythonScriptRun_ClickButton()
        {
            if (IsBusy) return;               // двойной клик
            IsBusy = true;
            try
            {
                //await RunPythonWithLogAsync();

                await RunPythonAsync();
            }
            finally
            {
                IsBusy = false;               // погасить индикатор
            }
        }

        private string _outputResult = "";
        public string OutputResult
        {
            get => _outputResult;
            set => this.RaiseAndSetIfChanged(ref _outputResult, value);
        }

        private async Task RunPythonAsync()
        {
            /* пути и папки */
            string appDir = AppContext.BaseDirectory;                 // bin\Debug\net8.0
            string pyExe = Path.Combine(appDir, "python_env", "Scripts", "python.exe");
            string script = Path.Combine(appDir, "diplomaApp.py");
            string cfgPath = Path.Combine(appDir, "config.json");
            string resDir = Path.Combine(appDir, "Results", "Images");

            Directory.CreateDirectory(resDir);

            /* JSON‑конфиг */
            var jsonOpts = new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };
            jsonOpts.Converters.Add(new DoubleWithDotConverter());
            string json = JsonSerializer.Serialize(Config, jsonOpts);
            await File.WriteAllTextAsync(cfgPath, json);

            /* ===== вычисляем следующий порядковый номер изображения ===== */
            int nextIdx = Directory.EnumerateFiles(resDir, "profile_*.png")
                                   .Select(Path.GetFileNameWithoutExtension)
                                   .Select(fn => int.TryParse(fn?.Split('_').Last(), out int n) ? n : 0)
                                   .DefaultIfEmpty()
                                   .Max() + 1;

            string tmpImage = Path.Combine(resDir, "profile.png");           // так пишет Python
            string finalImg = Path.Combine(resDir, $"profile_{nextIdx}.png"); // конечное имя

            /* запускаем Python */
            var psi = new ProcessStartInfo
            {
                FileName = pyExe,
                Arguments = $"-X utf8 \"{script}\" \"{cfgPath}\" \"{resDir}\"",
                WorkingDirectory = appDir,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var proc = Process.Start(psi);
            string stdout = await proc.StandardOutput.ReadToEndAsync();
            string stderr = await proc.StandardError.ReadToEndAsync();
            await proc.WaitForExitAsync();

            if (proc.ExitCode != 0) 
            {
                OutputResult = $"Скрипт завершился ошибкой:\n{stderr}";
                return;
            }

            string csvPath = Path.Combine(resDir, "results.csv");
            if (File.Exists(csvPath))
            {
                // CSV разделён точкой-с запятой; заменяем её на табуляцию для ровных колонок
                var lines = (await File.ReadAllLinesAsync(csvPath, Encoding.UTF8))
                           .Select(l => l.Replace(';', '\t'));
                OutputResult = string.Join(Environment.NewLine, lines);
            }
            else
            {
                OutputResult = "Файл results.csv не найден.";
            }
            /* переименовываем картинку + биндим */
            if (File.Exists(tmpImage))
            {
                if (File.Exists(finalImg)) File.Delete(finalImg);
                File.Move(tmpImage, finalImg);        // сохраняем с номером
                ResultBitmap = new Bitmap(finalImg);  // отдаём UI
            }
        }




        // ─────────── Физические параметры ───────────
        public void SetWaterDensity(double value) => Config.Physical.WaterDensity = value;
        public void SetSpecificHeatCapacity(double v) => Config.Physical.SpecificHeatCapacity = v;
        public void SetThermalConductivity(double v) => Config.Physical.ThermalConductivity = v;
        public void SetTEnv(double v) => Config.Physical.T_env = v;
        public void SetTIn(double v) => Config.Physical.T_in = v;
        public void SetTReq(double v) => Config.Physical.T_req = v;
        public void SetBetaEnv(double v) => Config.Physical.Beta_env = v;
        public void SetTauH(double v) => Config.Physical.Tau_h = v;

        // ─────────── Геометрия ───────────
        public void SetLength(double v) => Config.Geometry.Length_m = v;
        public void SetDint(double v) => Config.Geometry.D_int_m = v;
        public void SetLambdaFric(double v) => Config.Geometry.LambdaFric = v;

        // ─────────── Насос ───────────
        public void SetHStatic(double v) => Config.Pump.H_static_m = v;
        public void SetEtaPump(double v) => Config.Pump.Eta_pump = v;
        public void SetEtaMotor(double v) => Config.Pump.Eta_motor = v;

        // ─────────── Скорость ───────────
        public void SetVMin(double v) => Config.Speed.V_MIN = v;
        public void SetVMax(double v) => Config.Speed.V_MAX = v;
        public void SetMseg(int v) => Config.Speed.Mseg = v;

        // ─────────── Веса целевой функции ───────────
        public void SetAlphaJ(double v) => Config.Weights.Alpha_J = v;
        public void SetBetaJ(double v) => Config.Weights.Beta_J = v;
        public void SetGammaJ(double v) => Config.Weights.Gamma_J = v;
        public void SetDeltaT(double v) => Config.Weights.DeltaT = v;
        public void SetLambdaBand(double v) => Config.Weights.LambdaBand = v;
    }

    public class PhysicalParameters : ReactiveObject
    {
        private double _waterDensity = 1000.0;   // ρ
        public double WaterDensity
        {
            get => _waterDensity;
            set => this.RaiseAndSetIfChanged(ref _waterDensity, value);
        }

        private double _specificHeatCapacity = 4186.0;  // c_p
        public double SpecificHeatCapacity
        {
            get => _specificHeatCapacity;
            set => this.RaiseAndSetIfChanged(ref _specificHeatCapacity, value);
        }

        private double _thermalConductivity = 0.6;      // k
        public double ThermalConductivity
        {
            get => _thermalConductivity;
            set => this.RaiseAndSetIfChanged(ref _thermalConductivity, value);
        }

        private double _tEnv = 263.0;                    // K
        public double T_env
        {
            get => _tEnv;
            set => this.RaiseAndSetIfChanged(ref _tEnv, value);
        }

        private double _tIn = 348.0;
        public double T_in
        {
            get => _tIn;
            set => this.RaiseAndSetIfChanged(ref _tIn, value);
        }

        private double _tReq = 333.0;
        public double T_req
        {
            get => _tReq;
            set => this.RaiseAndSetIfChanged(ref _tReq, value);
        }

        private double _betaEnv = 1.0e-4;                // β_env
        public double Beta_env
        {
            get => _betaEnv;
            set => this.RaiseAndSetIfChanged(ref _betaEnv, value);
        }

        private double _tauH = 3.0;                      // τ, ч
        public double Tau_h
        {
            get => _tauH;
            set => this.RaiseAndSetIfChanged(ref _tauH, value);
        }
    }
    public class GeometryParameters : ReactiveObject
    {
        private double _length = 2_000.0;                // L
        public double Length_m
        {
            get => _length;
            set => this.RaiseAndSetIfChanged(ref _length, value);
        }

        private double _dInt = 0.273;                    // D_int
        public double D_int_m
        {
            get => _dInt;
            set => this.RaiseAndSetIfChanged(ref _dInt, value);
        }

        private double _lambdaFric = 0.02;               // λ_fric
        public double LambdaFric
        {
            get => _lambdaFric;
            set => this.RaiseAndSetIfChanged(ref _lambdaFric, value);
        }
    }
    public class PumpParameters : ReactiveObject
    {
        private double _hStatic = 55.0;
        public double H_static_m
        {
            get => _hStatic;
            set => this.RaiseAndSetIfChanged(ref _hStatic, value);
        }

        private double _etaPump = 0.74;
        public double Eta_pump
        {
            get => _etaPump;
            set => this.RaiseAndSetIfChanged(ref _etaPump, value);
        }

        private double _etaMotor = 0.92;
        public double Eta_motor
        {
            get => _etaMotor;
            set => this.RaiseAndSetIfChanged(ref _etaMotor, value);
        }
    }
    public class SpeedParameters : ReactiveObject
    {
        private double _vMin = 0.20;
        public double V_MIN
        {
            get => _vMin;
            set => this.RaiseAndSetIfChanged(ref _vMin, value);
        }

        private double _vMax = 2.00;
        public double V_MAX
        {
            get => _vMax;
            set => this.RaiseAndSetIfChanged(ref _vMax, value);
        }

        private int _mSeg = 60;
        public int Mseg
        {
            get => _mSeg;
            set => this.RaiseAndSetIfChanged(ref _mSeg, value);
        }
    }
    public class ObjectiveWeights : ReactiveObject
    {
        private double _alphaJ = 1.0;
        public double Alpha_J
        {
            get => _alphaJ;
            set => this.RaiseAndSetIfChanged(ref _alphaJ, value);
        }

        private double _betaJ = 2e-9;
        public double Beta_J
        {
            get => _betaJ;
            set => this.RaiseAndSetIfChanged(ref _betaJ, value);
        }

        private double _gammaJ = 8e4;
        public double Gamma_J
        {
            get => _gammaJ;
            set => this.RaiseAndSetIfChanged(ref _gammaJ, value);
        }

        private double _deltaT = 4.0;
        public double DeltaT
        {
            get => _deltaT;
            set => this.RaiseAndSetIfChanged(ref _deltaT, value);
        }

        private double _lambdaBand = 1e8;
        public double LambdaBand
        {
            get => _lambdaBand;
            set => this.RaiseAndSetIfChanged(ref _lambdaBand, value);
        }
    }
    public class SolverConfig
    {
        public PhysicalParameters Physical { get; set; } = new();
        public GeometryParameters Geometry { get; set; } = new();
        public PumpParameters Pump { get; set; } = new();
        public SpeedParameters Speed { get; set; } = new();
        public ObjectiveWeights Weights { get; set; } = new();
    }

    public sealed class DoubleWithDotConverter : JsonConverter<double>
    {
        public override double Read(ref Utf8JsonReader reader,
                                    Type typeToConvert,
                                    JsonSerializerOptions options)
            => reader.GetDouble();

        public override void Write(Utf8JsonWriter writer,
                                   double value,
                                   JsonSerializerOptions options)
        {
            // Формат: минимум ".0", дальше до 15 значащих цифр
            string formatted = value % 1 == 0
                ? value.ToString("0.0", CultureInfo.InvariantCulture)          // 55  →  "55.0"
                : value.ToString("0.###############", CultureInfo.InvariantCulture);

#if NET8_0_OR_GREATER
            writer.WriteRawValue(formatted, skipInputValidation: true);        // число, не строка
#else
        // Если сидите на < .NET 8: пишем как строку, а потом post‑процессом убираем кавычки,
        // или переходите на .NET 8, где есть WriteRawValue.
        writer.WriteStringValue(formatted);
#endif
        }
    }

}
