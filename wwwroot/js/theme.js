console.log("themeSwitcher.js loaded");

window.themeSwitcher = {
    toggleTheme: function () {
        const body = document.querySelector('body');
        if (body.classList.contains('dark-mode')) {
            body.classList.remove('dark-mode');
            localStorage.setItem('theme', 'light');
        } else {
            body.classList.add('dark-mode');
            localStorage.setItem('theme', 'dark');
        }
    },

    initTheme: function () {
        const savedTheme = localStorage.getItem('theme');
        if (savedTheme === 'dark') {
            document.querySelector('body').classList.add('dark-mode');
        } else {
            document.querySelector('body').classList.remove('dark-mode');
            if (!savedTheme) localStorage.setItem('theme', 'light');
        }
    }
};

document.addEventListener('DOMContentLoaded', window.themeSwitcher.initTheme);