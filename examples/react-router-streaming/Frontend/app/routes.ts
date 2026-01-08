import { type RouteConfig, index, route } from "@react-router/dev/routes";

export default [index("routes/home.tsx"), route("page", "routes/page.tsx")] satisfies RouteConfig;
