using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace ExamForge
{
    public partial class datagrid_usercontrol : UserControl
    {
        private ObservableCollection<ExamStructureRow> dataRows;

        public List<string> AllTestGroups { get; set; }
        public ObservableCollection<string> TestTypes { get; set; }

        private int totalItems = 0;
        private bool _suppressRowEvents = false;

        public datagrid_usercontrol()
        {
            InitializeComponent();

            AllTestGroups = new List<string> { "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X", "XI", "XII" };
            TestTypes = new ObservableCollection<string>
            {
                "Multiple Choice",
                "True or False",
                "Modified True or False",
                "Identification",
                "Enumeration",
                "Essay"
            };

            dataRows = new ObservableCollection<ExamStructureRow>();
            dataRows.CollectionChanged += DataRows_CollectionChanged;

            exam_structure_datagridview.ItemsSource = dataRows;
            this.DataContext = this;

            AddNewRow();
            UpdateEssayCountColumnVisibility();
        }

        private void DataRows_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                foreach (ExamStructureRow row in e.NewItems)
                {
                    row.PropertyChanged += Row_PropertyChanged;
                }
            }

            if (e.OldItems != null)
            {
                foreach (ExamStructureRow row in e.OldItems)
                {
                    row.PropertyChanged -= Row_PropertyChanged;
                }
            }

            NormalizeAndRebuild();
        }

        private void Row_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_suppressRowEvents) return;
            
            // Trigger full normalize + rebuild after any user change
            NormalizeAndRebuild();
        }

        /// <summary>
        /// SINGLE ATOMIC PASS: Normalize data (enforce rules), then validate against totalItems.
        /// This is the ONLY method that validates row data and shows warnings.
        /// Called after ANY state change: user edits, add/remove row, restore state, totalItems change.
        /// </summary>
        private void NormalizeAndRebuild()
        {
            if (_suppressRowEvents) return;

            _suppressRowEvents = true;

            // ==================== PHASE 1: NORMALIZE DATA ====================
            // Enforce all anti-redundancy rules and fact-check against totalItems.

            var seenGroups = new HashSet<string>();
            var occupiedNumbers = new HashSet<int>();

            foreach (var row in dataRows)
            {
                // Clear previous warnings
                row.Warning = "";
                var isEssay = string.Equals(row.TestType?.Trim(), "Essay", StringComparison.OrdinalIgnoreCase);

                if (!isEssay && !string.IsNullOrWhiteSpace(row.EssayCount))
                {
                    row.EssayCount = string.Empty;
                }

                // Rule 1: Unique Test Groups (first wins, later duplicates get warning)
                if (!string.IsNullOrWhiteSpace(row.TestGroup))
                {
                    string normalizedGroup = row.TestGroup.Trim();
                    if (seenGroups.Contains(normalizedGroup))
                    {
                        row.Warning = $"Test Group '{normalizedGroup}' already used.";
                    }
                    else
                    {
                        seenGroups.Add(normalizedGroup);
                    }
                }

                // Rule 2: Validate Start and End as numbers
                bool hasStart = int.TryParse(row.Start?.Trim(), out int start);
                bool hasEnd = int.TryParse(row.End?.Trim(), out int end);

                if (isEssay)
                {
                    if (string.IsNullOrWhiteSpace(row.EssayCount))
                        row.EssayCount = "1";

                    if (!int.TryParse(row.EssayCount.Trim(), out var essayCount) || essayCount < 1)
                    {
                        row.Warning = "# Essays must be a positive number for Essay type.";
                    }
                    else if (!int.TryParse(row.Points?.Trim(), out var essayPoints) || essayPoints < 1)
                    {
                        row.Warning = "Essay Points must be a positive number.";
                    }
                    else if (essayPoints < essayCount)
                    {
                        row.Warning = "Essay Points must be at least the number of essays.";
                    }
                    else
                    {
                        start = 1;
                        while (occupiedNumbers.Contains(start)) start++;

                        var computedEnd = start + essayPoints - 1;
                        row.Start = start.ToString();
                        row.End = computedEnd.ToString();
                        hasStart = true;
                        hasEnd = true;
                        end = computedEnd;
                    }
                }

                if (!isEssay && !string.IsNullOrWhiteSpace(row.Start) && !hasStart)
                {
                    row.Warning = "Start must be a valid number.";
                }
                else if (!isEssay && !string.IsNullOrWhiteSpace(row.End) && !hasEnd)
                {
                    row.Warning = "End must be a valid number.";
                }
                else if (!isEssay && hasEnd && !hasStart)
                {
                    row.Warning = "Enter Start before End.";
                }
                else if (hasStart && hasEnd)
                {
                    // Rule 3: FACT CHECK - Numbers must not exceed totalItems
                    if (totalItems > 0)
                    {
                        if (start < 1)
                        {
                            row.Warning = "Start must be at least 1.";
                        }
                        else if (start > totalItems)
                        {
                            row.Warning = $"Start ({start}) exceeds total items ({totalItems}).";
                        }
                        else if (end < 1)
                        {
                            row.Warning = "End must be at least 1.";
                        }
                        else if (end > totalItems)
                        {
                            row.Warning = $"End ({end}) exceeds total items ({totalItems}).";
                        }
                        else if (start > end)
                        {
                            row.Warning = "End must be ≥ Start.";
                        }
                        else
                        {
                            // Rule 4: Check for overlap with already-claimed numbers
                            bool hasOverlap = false;
                            for (int i = start; i <= end; i++)
                            {
                                if (occupiedNumbers.Contains(i))
                                {
                                    hasOverlap = true;
                                    break;
                                }
                            }

                            // Rule 5: Check for non-contiguous (skipping over occupied numbers)
                            bool hasGap = false;
                            for (int i = start + 1; i <= end; i++)
                            {
                                if (occupiedNumbers.Contains(i))
                                {
                                    hasGap = true;
                                    break;
                                }
                            }

                            if (hasOverlap)
                            {
                                row.Warning = $"Items {start}-{end} overlap with previous row.";
                            }
                            else if (hasGap)
                            {
                                row.Warning = $"Items {start}-{end} skip over assigned numbers.";
                            }
                            else
                            {
                                // Valid range - claim these numbers
                                for (int i = start; i <= end; i++)
                                {
                                    occupiedNumbers.Add(i);
                                }
                            }
                        }
                    }
                    else if (start > end)
                    {
                        row.Warning = "End must be ≥ Start.";
                    }
                    else
                    {
                        // No totalItems set yet, but range is internally valid
                        for (int i = start; i <= end; i++)
                        {
                            occupiedNumbers.Add(i);
                        }
                    }
                }
            }

            UpdateEssayCountColumnVisibility();
            _suppressRowEvents = false;
        }

        private void UpdateEssayCountColumnVisibility()
        {
            if (EssayCountColumn == null) return;

            var hasEssayRows = dataRows.Any(r => string.Equals(r.TestType?.Trim(), "Essay", StringComparison.OrdinalIgnoreCase));
            EssayCountColumn.Visibility = hasEssayRows ? Visibility.Visible : Visibility.Collapsed;
        }

        private void AddNewRow()
        {
            var newRow = new ExamStructureRow();
            dataRows.Add(newRow);
        }

        private void AddRow_Click(object sender, RoutedEventArgs e)
        {
            AddNewRow();
        }

        private void RemoveRow_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is ExamStructureRow row)
            {
                if (dataRows.Count > 1)
                {
                    dataRows.Remove(row);
                }
                else
                {
                    MessageBox.Show("Cannot remove the last row. At least one row must remain.",
                        "Cannot Remove",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
            }
        }

        public void SetTotalItems(int items)
        {
            if (totalItems == items) return; // Avoid unnecessary rebuild
            
            totalItems = items;
            NormalizeAndRebuild();
        }

        public void PopulateDataGrid(int numberOfItems)
        {
            totalItems = numberOfItems;

            if (dataRows.Count == 0)
            {
                AddNewRow();
            }
            else
            {
                NormalizeAndRebuild();
            }
        }

        public void ClearDataGrid()
        {
            _suppressRowEvents = true;
            dataRows.Clear();
            totalItems = 0;
            _suppressRowEvents = false;
            AddNewRow();
        }

        public object SaveState()
        {
            var savedData = new DataGridState
            {
                Rows = new List<ExamStructureRow>(),
                TotalItems = totalItems
            };

            foreach (var row in dataRows)
            {
                savedData.Rows.Add(new ExamStructureRow
                {
                    TestGroup = row.TestGroup,
                    TestType = row.TestType,
                    Start = row.Start,
                    End = row.End,
                    Points = row.Points,
                    EssayCount = row.EssayCount
                });
            }

            return savedData;
        }

        public void RestoreState(object state)
        {
            if (state is DataGridState savedData)
            {
                _suppressRowEvents = true;

                dataRows.Clear();
                
                // CRITICAL: Restore totalItems FIRST before adding rows
                totalItems = savedData.TotalItems;

                if (savedData.Rows.Count > 0)
                {
                    foreach (var row in savedData.Rows)
                    {
                        var newRow = new ExamStructureRow
                        {
                            TestGroup = row.TestGroup,
                            TestType = row.TestType,
                            Start = row.Start,
                            End = row.End,
                            Points = row.Points,
                            EssayCount = row.EssayCount
                        };
                        dataRows.Add(newRow);
                    }
                }
                else
                {
                    AddNewRow();
                }

                _suppressRowEvents = false;

                // CRITICAL: Normalize after restore to validate saved data
                NormalizeAndRebuild();
            }
        }

        public bool ValidateStructure(out string errorMessage)
        {
            errorMessage = "";
            HashSet<int> allNumbers = new HashSet<int>();
            HashSet<string> usedGroups = new HashSet<string>();

            foreach (var row in dataRows)
            {
                if (string.IsNullOrWhiteSpace(row.TestGroup) ||
                    string.IsNullOrWhiteSpace(row.TestType) ||
                    string.IsNullOrWhiteSpace(row.Start) ||
                    string.IsNullOrWhiteSpace(row.End) ||
                    string.IsNullOrWhiteSpace(row.Points))
                {
                    errorMessage = "All fields must be filled in each row.";
                    return false;
                }

                string normalizedGroup = row.TestGroup.Trim();
                if (usedGroups.Contains(normalizedGroup))
                {
                    errorMessage = $"Test Group '{normalizedGroup}' is used more than once.";
                    return false;
                }
                usedGroups.Add(normalizedGroup);

                if (!int.TryParse(row.Start.Trim(), out int start) || !int.TryParse(row.End.Trim(), out int end))
                {
                    errorMessage = "Start and End must be valid numbers.";
                    return false;
                }

                if (string.Equals(row.TestType?.Trim(), "Essay", StringComparison.OrdinalIgnoreCase))
                {
                    if (!int.TryParse(row.EssayCount?.Trim(), out var essayCount) || essayCount < 1)
                    {
                        errorMessage = "Essay rows must have a valid # Essays value (>= 1).";
                        return false;
                    }

                    if (!int.TryParse(row.Points.Trim(), out var essayPoints) || essayPoints < 1)
                    {
                        errorMessage = "Essay rows must have valid Points (>= 1).";
                        return false;
                    }

                    if (essayPoints < essayCount)
                    {
                        errorMessage = "Essay points must be greater than or equal to # Essays.";
                        return false;
                    }
                }

                if (start < 1 || end < 1)
                {
                    errorMessage = "Start and End must be at least 1.";
                    return false;
                }

                if (start > end)
                {
                    errorMessage = $"In Test Group {normalizedGroup}: Start ({start}) cannot be greater than End ({end}).";
                    return false;
                }

                if (totalItems > 0 && (start > totalItems || end > totalItems))
                {
                    errorMessage = $"In Test Group {normalizedGroup}: Range ({start}-{end}) exceeds total items ({totalItems}).";
                    return false;
                }

                for (int i = start; i <= end; i++)
                {
                    if (allNumbers.Contains(i))
                    {
                        errorMessage = $"Item number {i} is assigned to multiple test groups.";
                        return false;
                    }
                    allNumbers.Add(i);
                }

                if (!int.TryParse(row.Points.Trim(), out int points) || points <= 0)
                {
                    errorMessage = "Points must be a positive number.";
                    return false;
                }
            }

            if (totalItems > 0 && allNumbers.Count != totalItems)
            {
                errorMessage = $"Not all items are assigned. Expected {totalItems} items, but only {allNumbers.Count} are assigned.";
                return false;
            }

            return true;
        }
    }

    public class ExamStructureRow : INotifyPropertyChanged
    {
        private string _testGroup = "";
        private string _testType = "";
        private string _start = "";
        private string _end = "";
        private string _points = "";
        private string _essayCount = "1";
        private string _warning = "";

        public string TestGroup
        {
            get => _testGroup;
            set
            {
                if (_testGroup != value)
                {
                    _testGroup = value;
                    OnPropertyChanged(nameof(TestGroup));
                }
            }
        }

        public string EssayCount
        {
            get => _essayCount;
            set
            {
                if (_essayCount != value)
                {
                    _essayCount = value;
                    OnPropertyChanged(nameof(EssayCount));
                }
            }
        }

        public string TestType
        {
            get => _testType;
            set
            {
                if (_testType != value)
                {
                    _testType = value;
                    OnPropertyChanged(nameof(TestType));
                }
            }
        }

        public string Start
        {
            get => _start;
            set
            {
                if (_start != value)
                {
                    _start = value;
                    OnPropertyChanged(nameof(Start));
                }
            }
        }

        public string End
        {
            get => _end;
            set
            {
                if (_end != value)
                {
                    _end = value;
                    OnPropertyChanged(nameof(End));
                }
            }
        }

        public string Points
        {
            get => _points;
            set
            {
                if (_points != value)
                {
                    _points = value;
                    OnPropertyChanged(nameof(Points));
                }
            }
        }

        public string Warning
        {
            get => _warning;
            set
            {
                if (_warning != value)
                {
                    _warning = value;
                    OnPropertyChanged(nameof(Warning));
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class DataGridState
    {
        public List<ExamStructureRow> Rows { get; set; } = new List<ExamStructureRow>();
        public int TotalItems { get; set; }
    }
}

