/* =====================================
   common.js — 项目通用库
   包含：
   - api()   统一接口封装（自动 Token / 过期处理 / 异常处理）
   - toggleTheme() 主题切换
   - initTheme()   进入页面自动恢复主题
   ===================================== */

/* -------------------------------------
   API 封装（支持 GET/POST 自动 Token）
------------------------------------- */
async function api(url, method = "POST", body = null) {
    const token = localStorage.getItem("token");

    try {
        const res = await fetch(url, {
            method,
            headers: {
                "Content-Type": "application/json",
                ...(token ? { "Authorization": "Bearer " + token } : {})
            },
            body: body ? JSON.stringify(body) : null
        });

        // Token 失效（未授权 / 权限不足）
        if (res.status === 401 || res.status === 403) {
            alert("登录已过期，请重新登录");
            localStorage.removeItem("token");
            location.href = "login.html";
            return null;
        }

        // 服务器异常
        if (!res.ok) {
            if (res.status == 429) {
                alert("操作太频繁");
                return null;
            }
            alert("服务器异常：" + res.status);
            return null;
        }

        // 解析 JSON
        let data;
        try {
            data = await res.json();
        } catch (e) {
            alert("响应格式错误，请稍后再试");
            return null;
        }

        // 后端返回 code != 0
        if (data.code != 0) {
            alert(data.msg || "操作失败");
            return null;
        }

        return data;

    } catch (err) {
        console.error("网络异常：", err);
        alert("网络连接失败，请检查网络后重试");
        return null;
    }
}

function codeing() {
    alert("功能开发中,敬请期待");
}

/* -------------------------------------
   主题切换
------------------------------------- */
function toggleTheme() {
    document.body.classList.toggle("light-theme");
    localStorage.setItem(
        "theme",
        document.body.classList.contains("light-theme") ? "light" : "dark"
    );
}

/* -------------------------------------
   页面加载自动恢复主题
------------------------------------- */
function initTheme() {
    const saved = localStorage.getItem("theme");
    if (saved === "light") {
        document.body.classList.add("light-theme");
    }
}

// 页面加载时立即执行
window.addEventListener("DOMContentLoaded", initTheme);
