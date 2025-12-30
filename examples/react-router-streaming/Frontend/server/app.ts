import { createRequestHandler } from "react-router";
//@ts-ignore
import * as build from "virtual:react-router/server-build"

export default createRequestHandler(build)