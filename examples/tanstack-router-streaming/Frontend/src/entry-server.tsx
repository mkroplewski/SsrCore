import { createRequestHandler, defaultStreamHandler } from "@tanstack/react-router/ssr/server";
import { createRouter } from "./router";
export default async function render(request: Request) {
  const handler = createRequestHandler({ request, createRouter });
  return await handler(defaultStreamHandler);
}
