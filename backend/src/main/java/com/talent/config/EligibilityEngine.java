package com.talent.config;

import java.math.BigDecimal;
import java.util.ArrayList;
import java.util.List;
import java.util.Locale;
import java.util.Objects;
import java.util.Set;

import com.talent.shared.web.BusinessRuleException;

/**
 * A small, safe rule language for candidate eligibility (ADR-04). No scripting: only
 * AND / OR / NOT, parentheses and {@code operand operator value} conditions.
 *
 * <pre>
 * expr      := and ( OR and )*
 * and       := unary ( AND unary )*
 * unary     := NOT unary | '(' expr ')' | condition
 * condition := operand op value | operand (NOT)? IN '(' value (',' value)* ')'
 * operand   := ident | ident '(' args ')' '.' ident
 * op        := = | != | &gt; | &gt;= | &lt; | &lt;=
 * value     := number | 'string' | "string" | ident | TRUE | FALSE
 * </pre>
 */
public final class EligibilityEngine {

    private static final Set<String> KEYWORDS = Set.of("AND", "OR", "NOT", "IN", "TRUE", "FALSE");

    private EligibilityEngine() {
    }

    public sealed interface Node permits And, Or, Not, Condition {
    }

    record And(List<Node> parts) implements Node {
    }

    record Or(List<Node> parts) implements Node {
    }

    record Not(Node inner) implements Node {
    }

    record Operand(String name, List<Object> args, String property) {
        boolean isFunction() {
            return args != null;
        }

        @Override
        public String toString() {
            return isFunction() ? name + "(" + args + ")." + property : name;
        }
    }

    record Condition(Operand operand, String op, List<Object> values) implements Node {
    }

    /** Parses and validates; throws {@link BusinessRuleException} with the position of the first error. */
    public static Node parse(String expression) {
        if (expression == null || expression.isBlank()) {
            throw new BusinessRuleException("RULE_SYNTAX", "Rule expression is empty");
        }
        Parser p = new Parser(tokenize(expression));
        Node n = p.expr();
        if (!p.atEnd()) {
            throw p.error("Unexpected '" + p.peek().text + "'");
        }
        return n;
    }

    public static boolean evaluate(Node node, EligibilityFacts facts) {
        return switch (node) {
            case And a -> a.parts().stream().allMatch(p -> evaluate(p, facts));
            case Or o -> o.parts().stream().anyMatch(p -> evaluate(p, facts));
            case Not n -> !evaluate(n.inner(), facts);
            case Condition c -> condition(c, facts);
        };
    }

    public static boolean evaluate(String expression, EligibilityFacts facts) {
        return evaluate(parse(expression), facts);
    }

    private static boolean condition(Condition c, EligibilityFacts facts) {
        Operand o = c.operand();
        Object actual = o.isFunction() ? facts.function(o.name(), o.args(), o.property()) : facts.field(o.name());
        if (actual == null) {
            return false;
        }
        return switch (c.op()) {
            case "IN" -> c.values().stream().anyMatch(v -> equal(actual, v));
            case "NOT IN" -> c.values().stream().noneMatch(v -> equal(actual, v));
            case "=" -> equal(actual, c.values().get(0));
            case "!=" -> !equal(actual, c.values().get(0));
            default -> compare(actual, c.values().get(0), c.op());
        };
    }

    private static boolean equal(Object a, Object b) {
        BigDecimal na = number(a);
        BigDecimal nb = number(b);
        if (na != null && nb != null) {
            return na.compareTo(nb) == 0;
        }
        return Objects.toString(a).equalsIgnoreCase(Objects.toString(b));
    }

    private static boolean compare(Object a, Object b, String op) {
        BigDecimal na = number(a);
        BigDecimal nb = number(b);
        if (na == null || nb == null) {
            return false;
        }
        int cmp = na.compareTo(nb);
        return switch (op) {
            case ">" -> cmp > 0;
            case ">=" -> cmp >= 0;
            case "<" -> cmp < 0;
            case "<=" -> cmp <= 0;
            default -> throw new IllegalStateException(op);
        };
    }

    private static BigDecimal number(Object o) {
        if (o instanceof Number n) {
            return new BigDecimal(n.toString());
        }
        if (o instanceof Boolean) {
            return null;
        }
        try {
            return o == null ? null : new BigDecimal(o.toString());
        } catch (NumberFormatException e) {
            return null;
        }
    }

    // ---- tokenizer -------------------------------------------------------------------------

    enum Kind { IDENT, NUMBER, STRING, OP, LPAREN, RPAREN, COMMA, DOT, KEYWORD }

    record Token(Kind kind, String text, int pos) {
    }

    static List<Token> tokenize(String s) {
        List<Token> out = new ArrayList<>();
        int i = 0;
        while (i < s.length()) {
            char ch = s.charAt(i);
            if (Character.isWhitespace(ch)) {
                i++;
            } else if (ch == '(') {
                out.add(new Token(Kind.LPAREN, "(", i++));
            } else if (ch == ')') {
                out.add(new Token(Kind.RPAREN, ")", i++));
            } else if (ch == ',') {
                out.add(new Token(Kind.COMMA, ",", i++));
            } else if (ch == '.' && !(i + 1 < s.length() && Character.isDigit(s.charAt(i + 1)) && (out.isEmpty() || out.get(out.size() - 1).kind() != Kind.RPAREN))) {
                out.add(new Token(Kind.DOT, ".", i++));
            } else if (ch == '\'' || ch == '"') {
                int end = s.indexOf(ch, i + 1);
                if (end < 0) {
                    throw new BusinessRuleException("RULE_SYNTAX", "Unclosed string at position " + i);
                }
                out.add(new Token(Kind.STRING, s.substring(i + 1, end), i));
                i = end + 1;
            } else if ("<>=!".indexOf(ch) >= 0) {
                String two = i + 1 < s.length() ? s.substring(i, i + 2) : "";
                if (two.equals(">=") || two.equals("<=") || two.equals("!=")) {
                    out.add(new Token(Kind.OP, two, i));
                    i += 2;
                } else if (ch == '!') {
                    throw new BusinessRuleException("RULE_SYNTAX", "Unexpected '!' at position " + i);
                } else {
                    out.add(new Token(Kind.OP, String.valueOf(ch), i++));
                }
            } else if (Character.isDigit(ch) || ch == '-' || ch == '.') {
                int start = i++;
                while (i < s.length() && (Character.isDigit(s.charAt(i)) || s.charAt(i) == '.')) {
                    i++;
                }
                out.add(new Token(Kind.NUMBER, s.substring(start, i), start));
            } else if (Character.isLetter(ch) || ch == '_') {
                int start = i;
                while (i < s.length() && (Character.isLetterOrDigit(s.charAt(i)) || s.charAt(i) == '_')) {
                    i++;
                }
                String word = s.substring(start, i);
                String upper = word.toUpperCase(Locale.ROOT);
                out.add(KEYWORDS.contains(upper) ? new Token(Kind.KEYWORD, upper, start) : new Token(Kind.IDENT, word, start));
            } else {
                throw new BusinessRuleException("RULE_SYNTAX", "Unexpected character '" + ch + "' at position " + i);
            }
        }
        return out;
    }

    // ---- parser ----------------------------------------------------------------------------

    private static final class Parser {
        private final List<Token> tokens;
        private int i;

        Parser(List<Token> tokens) {
            this.tokens = tokens;
        }

        boolean atEnd() {
            return i >= tokens.size();
        }

        Token peek() {
            return atEnd() ? new Token(Kind.KEYWORD, "<end>", -1) : tokens.get(i);
        }

        boolean keyword(String k) {
            if (!atEnd() && peek().kind() == Kind.KEYWORD && peek().text().equals(k)) {
                i++;
                return true;
            }
            return false;
        }

        Token expect(Kind kind, String what) {
            if (atEnd() || peek().kind() != kind) {
                throw error("Expected " + what + " but found '" + peek().text() + "'");
            }
            return tokens.get(i++);
        }

        BusinessRuleException error(String msg) {
            int pos = peek().pos();
            return new BusinessRuleException("RULE_SYNTAX", msg + (pos >= 0 ? " at position " + pos : " at end of rule"));
        }

        Node expr() {
            List<Node> parts = new ArrayList<>(List.of(and()));
            while (keyword("OR")) {
                parts.add(and());
            }
            return parts.size() == 1 ? parts.get(0) : new Or(parts);
        }

        Node and() {
            List<Node> parts = new ArrayList<>(List.of(unary()));
            while (keyword("AND")) {
                parts.add(unary());
            }
            return parts.size() == 1 ? parts.get(0) : new And(parts);
        }

        Node unary() {
            if (keyword("NOT")) {
                return new Not(unary());
            }
            if (!atEnd() && peek().kind() == Kind.LPAREN) {
                i++;
                Node n = expr();
                expect(Kind.RPAREN, "')'");
                return n;
            }
            return condition();
        }

        Node condition() {
            Operand operand = operand();
            if (keyword("NOT")) {
                if (!keyword("IN")) {
                    throw error("Expected IN after NOT");
                }
                return new Condition(operand, "NOT IN", list());
            }
            if (keyword("IN")) {
                return new Condition(operand, "IN", list());
            }
            Token op = expect(Kind.OP, "a comparison operator");
            return new Condition(operand, op.text(), List.of(value()));
        }

        Operand operand() {
            String name = expect(Kind.IDENT, "a field name").text();
            if (!atEnd() && peek().kind() == Kind.LPAREN) {
                List<Object> args = list();
                expect(Kind.DOT, "'.' and a property after a function call");
                String prop = expect(Kind.IDENT, "a property name").text();
                return new Operand(name, args, prop);
            }
            return new Operand(name, null, null);
        }

        List<Object> list() {
            expect(Kind.LPAREN, "'('");
            List<Object> values = new ArrayList<>();
            if (!atEnd() && peek().kind() == Kind.RPAREN) {
                i++;
                return values;
            }
            values.add(value());
            while (!atEnd() && peek().kind() == Kind.COMMA) {
                i++;
                values.add(value());
            }
            expect(Kind.RPAREN, "')'");
            return values;
        }

        Object value() {
            Token t = peek();
            switch (t.kind()) {
                case NUMBER -> {
                    i++;
                    return new BigDecimal(t.text());
                }
                case STRING, IDENT -> {
                    i++;
                    return t.text();
                }
                case KEYWORD -> {
                    if (t.text().equals("TRUE") || t.text().equals("FALSE")) {
                        i++;
                        return Boolean.valueOf(t.text());
                    }
                    throw error("Expected a value but found '" + t.text() + "'");
                }
                default -> throw error("Expected a value but found '" + t.text() + "'");
            }
        }
    }
}
