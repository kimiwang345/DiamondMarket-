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
        function renderVIf() {
            app.querySelectorAll("[v-if]").forEach(node => {
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


        /* =========================
          v-for 渲染
          语法：v-for="item in items"
       ========================== */
        function renderVFor2() {
            app.querySelectorAll("[v-for]").forEach(templateNode => {

                const expr = templateNode.getAttribute("v-for").trim(); // item in selling
                const [itemName, arrName] = expr.split(" in ").map(s => s.trim());

                const arr = getValue(state, arrName);
                if (!Array.isArray(arr)) return;

                // 模板缓存
                if (!templateMap.has(templateNode)) {
                    templateMap.set(templateNode, templateNode.cloneNode(true));
                }

                const parent = templateNode.parentNode;
                const template = templateMap.get(templateNode);

                // 清理旧内容
                parent.querySelectorAll(`[data-vfor="${arrName}"]`).forEach(e => e.remove());

                // 渲染每个 item
                arr.forEach(item => {
                    const clone = template.cloneNode(true);
                    clone.removeAttribute("v-for");
                    clone.dataset.vfor = arrName;

                    /* ---------- 插值渲染 ---------- */
                    const walker = document.createTreeWalker(clone, NodeFilter.SHOW_TEXT);
                    while (walker.nextNode()) {
                        const node = walker.currentNode;
                        node.textContent = node.textContent.replace(/\{\{(.+?)\}\}/g, (_, key) => {
                            key = key.trim();

                            // item.xxx
                            if (key.startsWith(itemName + ".")) {
                                return getValue(item, key.replace(itemName + ".", "")) ?? "";
                            }
                            // 普通变量
                            return getValue(state, key) ?? "";
                        });
                    }

                    /* ---------- :class 渲染 ---------- */
                    clone.querySelectorAll("[\\:class]").forEach(el => {
                        const classExpr = el.getAttribute(":class");
                        const scope = Object.assign({}, state, { [itemName]: item });
                        const result = evalInScope(scope, classExpr);

                        if (typeof result === "object") {
                            Object.keys(result).forEach(cls => {
                                if (result[cls]) el.classList.add(cls);
                                else el.classList.remove(cls);
                            });
                        }
                    });


                    clone.querySelectorAll("[v-on\\:click]").forEach(btn => {
                        btn.__scope = Object.assign({}, state, {
                            [itemName.trim()]: item
                        });

                    });


                    parent.insertBefore(clone, templateNode);
                });

                templateNode.style.display = "none";
            });
        }

        function renderVFor() {
            app.querySelectorAll("[v-for]").forEach(templateNode => {
                const expr = templateNode.getAttribute("v-for").trim();
                const [itemName, arrName] = expr.split(" in ");

                const arr = getValue(state, arrName.trim());
                if (!Array.isArray(arr)) return;

                if (!templateMap.has(templateNode))
                    templateMap.set(templateNode, templateNode.cloneNode(true));

                const parent = templateNode.parentNode;
                const template = templateMap.get(templateNode);

                // 先清空旧渲染
                parent.querySelectorAll(`[data-vfor="${arrName.trim()}"]`).forEach(e => e.remove());

                arr.forEach(item => {
                    const clone = template.cloneNode(true);
                    clone.removeAttribute("v-for");
                    clone.dataset.vfor = arrName.trim();

                    /*-----------------------------------------
                     🎯 ① {{表达式}} 解析 (支持函数与运算)
                    ------------------------------------------*/
                    const walker = document.createTreeWalker(clone, NodeFilter.SHOW_TEXT);
                    while (walker.nextNode()) {
                        walker.currentNode.textContent = walker.currentNode.textContent.replace(/\{\{(.+?)\}\}/g, (_, code) => {
                            const scope = Object.assign({}, state, { [itemName.trim()]: item });
                            try {
                                return Function("with(this) { return " + code.trim() + "}").call(scope) ?? "";
                            } catch {
                                return "";
                            }
                        });
                    }

                    /*-----------------------------------------
                     🎯 ② v-on:click 动态执行，item 可直接使用
                    ------------------------------------------*/
                    clone.querySelectorAll("[v-on\\:click]").forEach(btn => {
                        const code = btn.getAttribute("v-on:click").trim();

                        btn.onclick = () => {
                            const scope = Object.assign({}, state, { [itemName.trim()]: item });

                            try {
                                // 如果是 copyText(item.order_no) 这种调用方式
                                if (code.includes("(")) {
                                    Function("with(this){ " + code + "}").call(scope);
                                } else {
                                    // 如果只是变量或表达式
                                    Function("with(this){ return " + code + "}").call(scope);
                                }
                            } catch (e) {
                                console.error("点击事件执行失败:", code, e);
                            }

                            renderAll(); // 保证视图同步更新
                        };
                    });

                    parent.insertBefore(clone, templateNode);
                });

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


        /* --------------------
           v-model
        -------------------- */
        function bindInputsOnce() {
            app.querySelectorAll("[v-model]").forEach(input => {
                const key = input.getAttribute("v-model");

                if (!initializedInputs.has(input)) {
                    const v = getValue(state, key);
                    if (v !== undefined) input.value = v;
                    initializedInputs.add(input);
                }

                input.addEventListener("input", e => {
                    setValue(state, key, e.target.value);
                    renderAll();
                });

            });
        }

        function bindEvents() {
            app.querySelectorAll("[v-on\\:click]").forEach(el => {
                if (el.__bound) return;  // 避免重复绑定

                const expr = el.getAttribute("v-on:click");

                el.addEventListener("click", e => {
                    const scope = el.__scope || state;

                    if (typeof scope[expr] === "function") {
                        scope[expr].call(scope, e);
                    } else {
                        Function("with(this){ " + expr + " }").call(scope);
                    }

                    renderAll();
                });



                el.__bound = true; // 标记避免重复绑定
            });
        }
        
        /* --------------------
           v-on:click
        -------------------- */
        function bindEventsOnce() {
            app.querySelectorAll("[v-on\\:click]").forEach(el => {
                const expr = el.getAttribute("v-on:click");

                el.addEventListener("click", e => {
                    // 方法名
                    if (typeof state[expr] === "function") {
                        state[expr].call(state, e);
                    } else {
                        // 表达式，如：mode='recycle'
                        try {
                            Function("with(this) { " + expr + " }").call(state);
                        } catch (err) {
                            console.error("事件表达式错误：", expr, err);
                        }
                    }

                    renderAll();
                });
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
