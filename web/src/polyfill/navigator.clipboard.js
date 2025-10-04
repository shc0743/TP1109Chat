if (window.WebView && window.WebView.writeClipboard && window.WebView.readClipboard) {
    navigator.clipboard.writeText = async function(text) {
        return window.WebView.writeClipboard(text);
    }
    navigator.clipboard.readText = async function() {
        return window.WebView.readClipboard();
    }
}