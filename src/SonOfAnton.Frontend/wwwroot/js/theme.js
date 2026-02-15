window.themeManager = {
    setTheme: function (theme) {
        document.documentElement.setAttribute('data-theme', theme);
        localStorage.setItem('theme', theme);
    },
    getTheme: function () {
        return localStorage.getItem('theme') || 'dark';
    },
    toggleTheme: function () {
        const currentTheme = this.getTheme();
        const newTheme = currentTheme === 'dark' ? 'light' : 'dark';
        this.setTheme(newTheme);
        return newTheme;
    },
    initialize: function () {
        const theme = this.getTheme();
        this.setTheme(theme);
    }
};

// Initialize on load
window.themeManager.initialize();
