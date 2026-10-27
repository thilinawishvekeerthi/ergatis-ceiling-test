package com.talent.shared.web;

import org.springframework.stereotype.Controller;
import org.springframework.web.bind.annotation.GetMapping;

/**
 * Serves the React single-page app for client-side routes such as {@code /career} or {@code /idp/12}.
 * Paths with a file extension, and API, actuator and docs paths, are not forwarded.
 */
@Controller
class SpaForwardController {

    @GetMapping({"/{path:^(?!api|actuator|v3|swagger-ui|error)[^.]*}", "/{path:^(?!api|actuator|v3|swagger-ui|error)[^.]*}/**"})
    String forward() {
        return "forward:/index.html";
    }
}
