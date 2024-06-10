import { defineConfig, loadEnv } from "vite";
import vue from "@vitejs/plugin-vue";
import { fileURLToPath, URL } from "url";

export default ({ mode }: { mode: string }) => {
    process.env = { ...process.env, ...loadEnv(mode, process.cwd(), "") };

    return defineConfig({
        plugins: [vue()],
        resolve: {
            alias: {
                "@": fileURLToPath(new URL("./src", import.meta.url)),
                "@a": fileURLToPath(new URL("./src/api", import.meta.url)),
                "@c": fileURLToPath(new URL("./src/components", import.meta.url)),
                "@v": fileURLToPath(new URL("./src/views", import.meta.url)),
            },
        },

        build: {
            outDir: "build",
            emptyOutDir: true,
            sourcemap: true,
        },
        define: {
            "process.env": process.env,
        },
        server: {
            proxy: {
                "/api": { target: "http://localhost:5000", secure: false },
            },
            port: 3001,
            strictPort: true,
            host: "0.0.0.0",
        },
    });
};
