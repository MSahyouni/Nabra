using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ERPUI.Helpers
{
    public partial class PaginationHelper<T> : ObservableObject
    {
        private List<T> _allItems = new();

        [ObservableProperty]
        private int _pageSize = 10;

        [ObservableProperty]
        private int _currentPage = 1;

        [ObservableProperty]
        private int _totalItems;

        [ObservableProperty]
        private int _totalPages = 1;

        [ObservableProperty]
        private bool _hasNextPage;

        [ObservableProperty]
        private bool _hasPreviousPage;

        [ObservableProperty]
        private string _displayRangeText = "\u200E0 عناصر";

        [ObservableProperty]
        private string _pageInfoText = "\u200Eصفحة 1 من 1";

        public ObservableCollection<T> PaginatedItems { get; } = new();

        public PaginationHelper(int pageSize = 10)
        {
            _pageSize = pageSize > 0 ? pageSize : 10;
        }

        public void SetSource(IEnumerable<T> items)
        {
            _allItems = items?.ToList() ?? new List<T>();
            CurrentPage = 1;
            Refresh();
        }

        public void Refresh()
        {
            TotalItems = _allItems.Count;
            TotalPages = (int)Math.Ceiling((double)TotalItems / PageSize);
            if (TotalPages < 1) TotalPages = 1;

            if (CurrentPage > TotalPages) CurrentPage = TotalPages;
            if (CurrentPage < 1) CurrentPage = 1;

            HasPreviousPage = CurrentPage > 1;
            HasNextPage = CurrentPage < TotalPages;

            int skip = (CurrentPage - 1) * PageSize;
            var pageSlice = _allItems.Skip(skip).Take(PageSize).ToList();

            PaginatedItems.Clear();
            foreach (var item in pageSlice)
            {
                PaginatedItems.Add(item);
            }

            int startItem = TotalItems == 0 ? 0 : skip + 1;
            int endItem = Math.Min(skip + PageSize, TotalItems);

            DisplayRangeText = $"\u200Eعرض {startItem}-{endItem} من إجمالي {TotalItems}";
            PageInfoText = $"\u200Eصفحة {CurrentPage} من {TotalPages}";
        }

        [RelayCommand]
        public void NextPage()
        {
            if (HasNextPage)
            {
                CurrentPage++;
                Refresh();
            }
        }

        [RelayCommand]
        public void PreviousPage()
        {
            if (HasPreviousPage)
            {
                CurrentPage--;
                Refresh();
            }
        }

        [RelayCommand]
        public void GoToPage(int page)
        {
            if (page >= 1 && page <= TotalPages)
            {
                CurrentPage = page;
                Refresh();
            }
        }

        public void Clear()
        {
            _allItems.Clear();
            PaginatedItems.Clear();
            TotalItems = 0;
            TotalPages = 1;
            CurrentPage = 1;
            HasNextPage = false;
            HasPreviousPage = false;
            DisplayRangeText = "\u200E0 عناصر";
            PageInfoText = "\u200Eصفحة 1 من 1";
        }
    }
}

