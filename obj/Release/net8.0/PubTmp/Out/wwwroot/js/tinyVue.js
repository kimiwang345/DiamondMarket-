/* ================================
   tinyVue.js — 极简双向绑定框架（增强版）
   ================================= */
(function (global) {

    function tinyVue(options) {
        const app = document.querySelector(options.el);
        const state = options.data || {};
        const methods = options.methods || {};

        // methods 合并入 state
        Object.keys(methods).forEach(name => {
            state[name] = methods[name];
        });

        const templateMap = new WeakMap();
        const initializedInputs = new WeakSet();

        function reactive(obj) {
            return new Proxy(obj, {
                get(target, key) {
                    const val = target[key];
                    if (typeof val === "object" && val !== null) {
                        return reactive(val);
                    }
                    return val;
                },
                set(target, key, value) {
                    target[key] = value;
                    renderAll();
                    return true;
                }
            });
        }

        /* --------------------
           工具函数
        -------------------- */
        function getValue(obj, path) {
            return path.split('.').reduce((o, k) => (o ? o[k] : undefined), obj);
        }

        function setValue(obj, path, value) {
            const keys = path.split('.');
            const last = keys.pop();
            const target = keys.reduce((o, k) => o[k], obj);
            target[last] = value;
        }

        function evalInScope(scope, expr) {
            try {
                return Function("with(this) { return " + expr + "}").call(scope);
            } catch {
                return null;
            }
        }

        /* --------------------
           v-if
        -------------------- */
        function renderVIf2() {
            app.querySelectorAll("[v-if]").forEach(node => {
                const expr = node.getAttribute("v-if");
                const ok = evalInScope(state, expr);
                node.style.display = ok ? "" : "none";
            });
        }

        // 只处理不在 v-for 克隆内部的 v-if
        function renderVIf() {
            app.querySelectorAll("[v-if]").forEach(node => {
                // 🔥 如果这个节点在 data-vfor 下面，说明已经在 renderVFor 里处理过，直接跳过
                if (node.closest("[data-vfor]")) return;

                const expr = node.getAttribute("v-if");
                const ok = evalInScope(state, expr);
                node.style.display = ok ? "" : "none";
            });
        }


        /* --------------------
           :class
        -------------------- */
        function renderBindings() {
            app.querySelectorAll("[\\:class]").forEach(node => {
                const expr = node.getAttribute(":class");
                const result = evalInScope(state, expr);

                if (typeof result === "object" && result != null) {
                    Object.keys(result).forEach(cls => {
                        if (result[cls]) node.classList.add(cls);
                        else node.classList.remove(cls);
                    });
                }
            });
        }
        /* =============================
       额外补充：处理 for 外 :src
       ============================= */
        function renderGlobalSrc() {
            app.querySelectorAll("[\\:src]").forEach(el => {
                // ⚠ 如果此节点属于 v-for 渲染品 → 跳过
                if (el.closest("[data-vfor]")) return;

                const expr = el.getAttribute(":src").trim();
                try {
                    const val = Function("with(this){return " + expr + "}").call(state);
                    if (val) el.setAttribute("src", val);
                    else el.removeAttribute("src");
                } catch (e) { }
            });
        }


        /* --------------------
           {{ 插值 }}（修复版）
        -------------------- */
        function renderText() {
            const walker = document.createTreeWalker(app, NodeFilter.SHOW_TEXT);

            while (walker.nextNode()) {
                const node = walker.currentNode;

                // 保存原始模板
                if (!templateMap.has(node)) {
                    templateMap.set(node, node.textContent);
                }

                let raw = templateMap.get(node);

                // 替换所有 {{ xxx }}
                node.textContent = raw.replace(/\{\{(.+?)\}\}/g, (_, key) => {
                    key = key.trim();
                    return getValue(state, key) ?? "";
                });
            }
        }
        function renderVFor() {
            app.querySelectorAll("[v-for]").forEach(templateNode => {
                const expr = templateNode.getAttribute("v-for").trim(); // 例： item in withdrawList
                const [itemNameRaw, arrNameRaw] = expr.split(" in ");
                const itemName = itemNameRaw.trim();
                const arrName = arrNameRaw.trim();

                const arr = getValue(state, arrName);
                if (!Array.isArray(arr)) return;

                // 缓存原始模板
                if (!templateMap.has(templateNode)) {
                    templateMap.set(templateNode, templateNode.cloneNode(true));
                }
                const parent = templateNode.parentNode;
                const template = templateMap.get(templateNode);

                // 清空旧渲染
                parent.querySelectorAll(`[data-vfor="${arrName}"]`).forEach(e => e.remove());

                // === 循环渲染 ===
                arr.forEach(item => {
                    // ⭐ 统一作用域：state + 当前 item
                    const scope = Object.assign({}, state, { [itemName]: item });

                    const clone = template.cloneNode(true);
                    clone.removeAttribute("v-for");
                    clone.dataset.vfor = arrName;

                    /* ---------- 1. 处理 {{ }} 表达式 ---------- */
                    const walker = document.createTreeWalker(clone, NodeFilter.SHOW_TEXT);
                    while (walker.nextNode()) {
                        const node = walker.currentNode;
                        node.textContent = node.textContent.replace(/\{\{(.+?)\}\}/g, (_, code) => {
                            code = code.trim();
                            try {
                                return Function("with(this){ return " + code + "}").call(scope) ?? "";
                            } catch (e) {
                                console.warn("v-for text 解析失败：", code, e);
                                return "";
                            }
                        });
                    }

                    /* ---------- 2. 处理动态属性 :src / :href / :class / :title 等 ---------- */
                    clone.querySelectorAll("*").forEach(el => {
                        [...el.attributes].forEach(attr => {
                            if (!attr.name.startsWith(":")) return;

                            const realAttr = attr.name.slice(1);   // 去掉前面的 :
                            const code = attr.value.trim();

                            try {
                                const val = Function("with(this){ return " + code + "}").call(scope);

                                if (realAttr === "class" && val && typeof val === "object") {
                                    // :class="{ active: item.status===2 }"
                                    Object.keys(val).forEach(cls => {
                                        if (val[cls]) el.classList.add(cls);
                                        else el.classList.remove(cls);
                                    });
                                } else if (val == null || val === false) {
                                    el.removeAttribute(realAttr);
                                } else {
                                    el.setAttribute(realAttr, val);
                                }
                            } catch (e) {
                                console.warn("v-for :attr 解析失败：", code, e);
                            }
                        });
                    });

                    /* ---------- 3. 处理 v-if（这里用的就是上面那个 scope） ---------- */
                    clone.querySelectorAll("[v-if]").forEach(el => {
                        const cond = el.getAttribute("v-if");

                        try {
                            const show = Function("with(this){ return " + cond + "}").call(scope);
                            // 🔥 关键修复 - 优先覆盖模板带的 display:none
                            el.style.display = show ? "" : "none";
                        } catch (e) {
                            el.style.display = "none";
                        }
                    });


                    /* ---------- 4. 处理 v-on:click，支持 copyText(item.order_no) 这种写法 ---------- */
                    clone.querySelectorAll("[v-on\\:click]").forEach(btn => {
                        const clickCode = btn.getAttribute("v-on:click").trim();

                        btn.onclick = (ev) => {
                            try {
                                // 如果刚好是方法名，比如 v-on:click="someMethod"
                                if (typeof scope[clickCode] === "function") {
                                    scope[clickCode].call(scope, item, ev);
                                } else {
                                    // 表达式 / 函数调用，如 copyText(item.order_no)
                                    Function("with(this){ " + clickCode + " }").call(scope);
                                }
                            } catch (e) {
                                console.error("v-for click 执行失败：", clickCode, e);
                            }

                            renderAll(); // 点击后重新渲染，保证状态刷新到 DOM
                        };
                    });

                    parent.insertBefore(clone, templateNode);
                });

                // 模板本身隐藏
                 templateNode.style.display = "none";

            });
        }



        function renderAll() {
            renderVFor();
            renderVIf();
            renderBindings();
            bindEvents();
            renderText();
            bindInputs();
            renderGlobalSrc();

        }


        function bindInputs() {
            app.querySelectorAll("[v-model]").forEach(input => {
                const key = input.getAttribute("v-model");

                // ---- UI ← state（反向同步）----
                const val = getValue(state, key);
                if (input.type === "checkbox") {
                    input.checked = Boolean(val);
                } else if (input.value !== val) {
                    input.value = val ?? "";
                }

                // ---- state ← UI ----
                if (!initializedInputs.has(input)) {
                    initializedInputs.add(input);

                    input.addEventListener("input", e => {
                        let v = e.target.value;

                        if (input.type === "number") {
                            v = e.target.value ? Number(e.target.value) : null;
                        }

                        if (input.type === "checkbox") {
                            v = e.target.checked;
                        }

                        setValue(state, key, v);
                        renderAll();
                    });
                }
            });
        }


        function bindEvents() {
            app.querySelectorAll("[v-on\\:click]").forEach(el => {

                // 🔥 如果是 v-for 克隆的元素（已经注入 item 作用域）, 不重新绑定
                if (el.__scope) return;

                // 🔥 已绑定过就不要再次绑定
                if (el.__bound) return;

                const expr = el.getAttribute("v-on:click");

                el.addEventListener("click", e => {
                    const scope = state;

                    if (typeof scope[expr] === "function") {
                        scope[expr].call(scope, e);
                    } else {
                        try {
                            Function("with(this){ " + expr + " }").call(scope);
                        } catch (e) {
                        }
                    }

                    renderAll();
                });

                el.__bound = true; // 标记已绑定
            });
        }



        // 初始化
        bindInputs();
        // bindEventsOnce();

        // 延迟 0ms，等待 DOM 完全解析后再渲染一次（关键）
        setTimeout(() => {
            renderAll();
        }, 500);
        // 暴露一个手动刷新方法
        state.$forceUpdate = renderAll;
        return state;
    }

    global.tinyVue = tinyVue;

})(window);
