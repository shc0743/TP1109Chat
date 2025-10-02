#include <winrt/Windows.Foundation.h>
#include <winrt/Windows.Web.UI.h>
#include <winrt/Windows.Web.UI.Interop.h>

// 启用WinRT支持
using namespace winrt;
using namespace Windows::Foundation;
using namespace Windows::Web::UI;
using namespace Windows::Web::UI::Interop;

class WinRTWebViewApp {
private:
    winrt::Windows::Web::UI::IWebViewControl m_webView{ nullptr };

public:
    HRESULT Initialize(HWND hwnd) {
        // 初始化WinRT
        init_apartment(apartment_type::single_threaded);

        // 创建WebView
        auto process = WebViewControlProcess();
        auto op = process.CreateWebViewControlAsync(
            reinterpret_cast<int64_t>(hwnd),
            Rect{ 0, 0, 800, 600 }
        );

        // 等待创建完成
        if (op.Status() != AsyncStatus::Completed) {
            op.get(); // 阻塞等待
        }

        m_webView = op.GetResults();
        m_webView.Source(Uri(L"https://baidu.com"));

        return S_OK;
    }
};

