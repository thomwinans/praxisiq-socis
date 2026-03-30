window.authInterop = {
    getToken: function (key) {
        return localStorage.getItem(key);
    },
    setToken: function (key, value) {
        localStorage.setItem(key, value);
    },
    removeToken: function (key) {
        localStorage.removeItem(key);
    }
};
