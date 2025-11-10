// JWT Helper Functions
window.JwtHelper = (function () {
    'use strict';

    const TOKEN_KEY = 'token';
    const REFRESH_TOKEN_KEY = 'refreshToken';
    const USER_KEY = 'user';

    // Get token from localStorage
    function getToken() {
        return localStorage.getItem(TOKEN_KEY);
    }

    // Get refresh token from localStorage
    function getRefreshToken() {
        return localStorage.getItem(REFRESH_TOKEN_KEY);
    }

    // Save tokens to localStorage and cookies
    function saveTokens(token, refreshToken) {
        if (!token || !refreshToken) {
            console.error('Invalid tokens provided');
            return;
        }

        localStorage.setItem(TOKEN_KEY, token);
        localStorage.setItem(REFRESH_TOKEN_KEY, refreshToken);

        // Also save to cookies for server-side access
        const tokenExpiry = 3600; // 1 hour (seconds)
        const refreshExpiry = 604800; // 7 days (seconds)

        const isSecure = window.location.protocol === 'https:';

        // Use encodeURIComponent to avoid characters breaking cookie format
        const encodedToken = encodeURIComponent(token);
        const encodedRefresh = encodeURIComponent(refreshToken);

        const tokenExpires = new Date(Date.now() + tokenExpiry * 1000).toUTCString();
        const refreshExpires = new Date(Date.now() + refreshExpiry * 1000).toUTCString();

        // Build cookie strings. Only add Secure attribute when on HTTPS (so cookies persist on HTTP dev servers)
        let tokenCookie = `${TOKEN_KEY}=${encodedToken}; path=/; max-age=${tokenExpiry}; expires=${tokenExpires}; SameSite=Lax`;
        let refreshCookie = `${REFRESH_TOKEN_KEY}=${encodedRefresh}; path=/; max-age=${refreshExpiry}; expires=${refreshExpires}; SameSite=Lax`;

        if (isSecure) {
            tokenCookie += '; Secure';
            refreshCookie += '; Secure';
        }

        // Set cookies
        document.cookie = tokenCookie;
        document.cookie = refreshCookie;

        console.log('Tokens saved successfully');
    }

    // Clear tokens from localStorage and cookies
    function clearTokens() {
        localStorage.removeItem(TOKEN_KEY);
        localStorage.removeItem(REFRESH_TOKEN_KEY);
        localStorage.removeItem(USER_KEY);

        // Clear cookies (use same Secure logic as save)
        const isSecure = window.location.protocol === 'https:';
        // Standard past date for cookie expiry
        const expiresPast = 'Thu, 01 Jan 1970 00:00:00 GMT';

        let tokenClear = `${TOKEN_KEY}=; path=/; max-age=0; expires=${expiresPast}; SameSite=Lax`;
        let refreshClear = `${REFRESH_TOKEN_KEY}=; path=/; max-age=0; expires=${expiresPast}; SameSite=Lax`;

        if (isSecure) {
            tokenClear += '; Secure';
            refreshClear += '; Secure';
        }

        document.cookie = tokenClear;
        document.cookie = refreshClear;

        console.log('Tokens cleared');
    }

    // Get user from localStorage
    function getUser() {
        try {
            const userJson = localStorage.getItem(USER_KEY);
            return userJson ? JSON.parse(userJson) : null;
        } catch (e) {
            console.error('Error parsing user data:', e);
            return null;
        }
    }

    // Save user to localStorage
    function saveUser(user) {
        if (!user) {
            console.error('Invalid user data');
            return;
        }

        try {
            localStorage.setItem(USER_KEY, JSON.stringify(user));
            console.log('User data saved');
        } catch (e) {
            console.error('Error saving user data:', e);
        }
    }

    // Check if user is authenticated
    function isAuthenticated() {
        const token = getToken();
        if (!token) {
            return false;
        }

        // Check if token is expired
        try {
            const payload = parseJwt(token);
            if (!payload || !payload.exp) {
                return false;
            }

            const exp = payload.exp * 1000; // Convert to milliseconds
            const now = Date.now();

            // Token is valid if expiry is in the future
            return now < exp;
        } catch (e) {
            console.error('Error checking authentication:', e);
            return false;
        }
    }

    // Parse JWT token
    function parseJwt(token) {
        try {
            const base64Url = token.split('.')[1];
            if (!base64Url) {
                throw new Error('Invalid token format');
            }

            const base64 = base64Url.replace(/-/g, '+').replace(/_/g, '/');
            const jsonPayload = decodeURIComponent(
                atob(base64)
                    .split('')
                    .map(function (c) {
                        return '%' + ('00' + c.charCodeAt(0).toString(16)).slice(-2);
                    })
                    .join('')
            );

            return JSON.parse(jsonPayload);
        } catch (e) {
            console.error('Error parsing JWT:', e);
            return null;
        }
    }

    // Get user roles from token
    function getUserRoles() {
        const token = getToken();
        if (!token) return [];

        try {
            const payload = parseJwt(token);
            if (!payload) return [];

            // ASP.NET Core stores roles in 'http://schemas.microsoft.com/ws/2008/06/identity/claims/role'
            const roleClaim = 'http://schemas.microsoft.com/ws/2008/06/identity/claims/role';

            if (payload[roleClaim]) {
                // Can be string or array
                return Array.isArray(payload[roleClaim])
                    ? payload[roleClaim]
                    : [payload[roleClaim]];
            }

            return [];
        } catch (e) {
            console.error('Error getting user roles:', e);
            return [];
        }
    }

    // Check if user has a specific role
    function hasRole(roleName) {
        const roles = getUserRoles();
        return roles.includes(roleName);
    }

    // Refresh token
    async function refreshToken() {
        const token = getToken();
        const refreshTokenValue = getRefreshToken();

        if (!token || !refreshTokenValue) {
            console.error('No tokens found for refresh');
            return false;
        }

        try {
            const response = await fetch('/api/auth/refresh-token', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify({
                    token: token,
                    refreshToken: refreshTokenValue
                })
            });

            const data = await response.json();

            if (data.success && data.token && data.refreshToken) {
                saveTokens(data.token, data.refreshToken);
                if (data.user) {
                    saveUser(data.user);
                }
                console.log('Token refreshed successfully');
                return true;
            } else {
                console.warn('Token refresh failed:', data.message);
                clearTokens();
                return false;
            }
        } catch (error) {
            console.error('Error refreshing token:', error);
            clearTokens();
            return false;
        }
    }

    // Logout
    function logout() {
        // Call logout API
        fetch('/api/auth/logout', {
            method: 'POST',
            headers: {
                'Authorization': 'Bearer ' + getToken()
            }
        }).catch(err => console.error('Logout API error:', err));

        // Clear tokens and redirect
        clearTokens();
        window.location.href = '/auth/login';
    }

    // Setup AJAX defaults to include JWT token
    function setupAjaxDefaults() {
        if (typeof $ !== 'undefined' && $.ajaxSetup) {
            $.ajaxSetup({
                beforeSend: function (xhr, settings) {
                    // Skip auth header for login/register endpoints
                    if (settings.url.includes('/api/auth/login') ||
                        settings.url.includes('/api/auth/register')) {
                        return;
                    }

                    const token = getToken();
                    if (token) {
                        xhr.setRequestHeader('Authorization', 'Bearer ' + token);
                    }
                },
                error: function (xhr, status, error) {
                    if (xhr.status === 401) {
                        // Unauthorized - try to refresh token
                        refreshToken().then(success => {
                            if (!success) {
                                // Refresh failed - redirect to login
                                console.warn('Session expired, redirecting to login');
                                logout();
                            }
                        });
                    }
                }
            });
        }
    }

    // Check authentication and redirect if needed
    function requireAuth() {
        if (!isAuthenticated()) {
            console.warn('Authentication required, redirecting to login');
            window.location.href = '/auth/login';
            return false;
        }
        return true;
    }

    // Update UI based on authentication status
    function updateAuthUI() {
        const user = getUser();
        const isAuth = isAuthenticated();
        const roles = getUserRoles();
        const isAdmin = hasRole('Admin');

        if (isAuth && user) {
            // User is logged in
            // Show controls for any authenticated user
            $('.auth-only').show();
            $('.guest-only').hide();

            // Show admin-only and auth-required only for admins
            if (isAdmin) {
                $('.admin-only').show();
                $('.auth-required').show();
            } else {
                $('.admin-only').hide();
                $('.auth-required').hide();
            }

            // Update user info
            $('#authUserName').text(user.fullName || user.email || 'User');
            $('#authUserEmail').text(user.email || '');

            // Update avatar
            const initials = getInitials(user.fullName || user.email || 'U');
            $('#userAvatar').text(initials);

            console.log('User authenticated:', user.email, 'Roles:', roles);
        } else {
            // User is not logged in
            $('.auth-required').hide();
            $('.auth-only').hide();
            $('.admin-only').hide();
            $('.guest-only').show();
            console.log('User not authenticated');
        }
    }

    // Get initials from name
    function getInitials(name) {
        if (!name) return 'U';
        const parts = name.split(' ').filter(p => p.length > 0);
        if (parts.length >= 2) {
            return (parts[0][0] + parts[parts.length - 1][0]).toUpperCase();
        }
        return name.substring(0, 2).toUpperCase();
    }

    // Initialize on page load
    function init() {
        setupAjaxDefaults();
        updateAuthUI();

        // Handle logout button
        $(document).on('click', '.logout-btn', function (e) {
            e.preventDefault();
            logout();
        });

        // Auto-refresh token before it expires
        if (isAuthenticated()) {
            const token = getToken();
            const payload = parseJwt(token);

            if (payload && payload.exp) {
                const expiryTime = payload.exp * 1000;
                const now = Date.now();
                const timeUntilExpiry = expiryTime - now;

                // Refresh 5 minutes before expiry
                const refreshTime = timeUntilExpiry - (5 * 60 * 1000);

                if (refreshTime > 0) {
                    setTimeout(() => {
                        console.log('Auto-refreshing token...');
                        refreshToken();
                    }, refreshTime);
                }
            }
        }
    }

    // Initialize when DOM is ready
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }

    // Public API
    return {
        getToken: getToken,
        getRefreshToken: getRefreshToken,
        saveTokens: saveTokens,
        clearTokens: clearTokens,
        getUser: getUser,
        saveUser: saveUser,
        isAuthenticated: isAuthenticated,
        parseJwt: parseJwt,
        getUserRoles: getUserRoles,
        hasRole: hasRole,
        refreshToken: refreshToken,
        logout: logout,
        setupAjaxDefaults: setupAjaxDefaults,
        requireAuth: requireAuth,
        updateAuthUI: updateAuthUI
    };
})();