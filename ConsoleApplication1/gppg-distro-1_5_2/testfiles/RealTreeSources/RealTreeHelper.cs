
//
//  This is the helper file for the RealTree example
//  program.  It is an example program for use of the
//  GPPG parser generator.  Copyright (c) K John Gough 2012
//

using System;
using System.Collections.Generic;
using System.Text;
using QUT.Gppg;

namespace RealTree {
    //
    // There are several classes defined here.
    //
    // The parser class is named Parser (the gppg default),
    // and contains a handwritten scanner with class name
    // RealTree.Parser.Lexer.
    //
    // The Parser class is partial, so that most of the 
    // handwritten code may be placed in this file in a
    // second part of the class definition.
    //
    // This file also contains the class hierarchy for the
    // semantic action class RealTree.Node
    //
    // The token enumeration RealTree.Parser.Tokens (also
    // the gppg default) is generated by gppg.
    //
    internal partial class Parser {

        // 
        // GPPG does not create a default parser constructor
        // Most applications will have a parser type with other
        // fields such as error handlers etc.  Here is a minimal
        // version that just adds the default scanner object.
        // 

        Parser( Lexer s ) : base( s ) { }

        internal Node[] regs = new Node[26];

        // ==================================================================================

        static void Main(string[] args) {
            System.IO.TextReader reader;
            if (args.Length > 0)
                reader = new System.IO.StreamReader( args[0] );
            else
                reader = System.Console.In;

            Parser parser = new Parser( new Lexer( reader ) );
            Console.WriteLine( "RealCalc expression evaluator, type ^C to exit, help for help" );
            parser.Parse();
        }

        // ==================================================================================
        //  Version for real arithmetic.  YYSTYPE is RealTree.Node
        //  This version uses an embedded scanner rather than a gplex-generated one.
        //
        class Lexer : QUT.Gppg.AbstractScanner<RealTree.Node, LexLocation> {
            private System.IO.TextReader reader;
            private StringBuilder text = new StringBuilder();

            public Lexer( System.IO.TextReader reader ) {
                this.reader = reader;
            }

            private LexLocation singleton = new LexLocation();
            public override LexLocation yylloc {
                get { return singleton; }
            }

            public override int yylex() {
                char ch;
                char peek;
                int ord = reader.Read();
                //this.text.Clear();
                this.text.Length = 0;
                //
                // Must check for EOF
                //
                if (ord == -1)
                    return (int)Tokens.EOF;
                else
                    ch = (char)ord;

                if (ch == '\n')
                    return (int)Tokens.EOL;
                else if (char.IsWhiteSpace( ch )) { // Skip white space
                    while (char.IsWhiteSpace( peek = (char)reader.Peek() )) {
                        ord = reader.Read();
                        if (ord == (int)'\n')
                            return (int)Tokens.EOL; ;
                    }
                    return yylex();
                }
                else if (char.IsDigit( ch )) {
                    text.Append( ch );
                    while (char.IsDigit( peek = (char)reader.Peek() ))
                        text.Append( (char)reader.Read() );
                    if ((peek = (char)reader.Peek()) == '.')
                        text.Append( (char)reader.Read() );
                    while (char.IsDigit( peek = (char)reader.Peek() ))
                        text.Append( (char)reader.Read() );
                    try {
                        yylval = Parser.MakeConstLeaf( double.Parse( text.ToString() ) );
                        return (int)Tokens.LITERAL;
                    }
                    catch (FormatException) {
                        this.yyerror( "Illegal number \"{0}\"", text );
                        return (int)Tokens.error;
                    }
                }
                else if (char.IsLetter( ch )) {
                    text.Append( char.ToLower( ch ) );
                    while (char.IsLetter( peek = (char)reader.Peek() ))
                        text.Append( char.ToLower( (char)reader.Read() ) );
                    switch (text.ToString()) {
                        case "eval":
                            return (int)Tokens.EVAL;
                        case "exit":
                            return (int)Tokens.EXIT;
                        case "help":
                            return (int)Tokens.HELP;
                        case "reset":
                            return (int)Tokens.RESET;
                        case "print":
                            return (int)Tokens.PRINT;
                        default:
                            if (text.Length == 1) {
                                yylval = Parser.MakeIdLeaf( text[0] );
                                return (int)Tokens.LETTER;
                            }
                            else {
                                this.yyerror( "Illegal name \"{0}\"", text );
                                return (int)Tokens.error;
                            }
                    }
                }
                else
                    switch (ch) {
                        case '.': case '+': case '-': case '*':
                        case '/': case '(': case ')': case '%': case '=':
                            return ch;
                        case ';':
                            return (int)Tokens.EOL; ; // semicolon treated as command separator.
                        default:
                            yyerror( "Illegal character '{0}'", ch );
                            return yylex();
                    }
            }

            public override void yyerror( string format, params object[] args ) {
                Console.Error.WriteLine( format, args );
            }
        }
        // ==================================================================================
        //  End of Lexer class definition.
        // ==================================================================================

        //
        // Now the node factory methods
        //
        public static Node MakeBinary( NodeTag tag, Node lhs, Node rhs ) {
            return new Binary( tag, lhs, rhs );
        }

        public static Node MakeUnary(NodeTag tag, Node child) {
            return new Unary( tag, child );
        }

        public static Node MakeIdLeaf(char n) {
            return new Leaf( n );
        }

        public static Node MakeConstLeaf(double v) {
            return new Leaf( v );
        }

        // ==================================================================================
        // And the semantic helpers
        // ==================================================================================
        private void PrintHelp() {
            Console.WriteLine(
@"RealTree: there are 26 variables named 'a' .. 'z'. Names are case insensitive,
and expressions may reference literals or variables. Variables are either empty
or contain expression trees. Literals are a trivial case of a valid tree.
Commands are separated by newlines, or by semicolons ';'.  Valid commands are --
    > help              // print this notice.
    > exit              // exit the program. ^C works as well.
    > print             // prints the value of each valid variable.
    > reset             // clears all variables
    > eval expression   // evaluate the expression
    > eval x            // x is a variable containing an expression tree
    > x = expression    // store the expression tree in variable x.");
        }

        private void ClearRegisters() {
            for (int i = 0; i < regs.Length; i++)
                regs[i] = null;
        }

        private void PrintRegisters() {
            for (int i = 0; i < regs.Length; i++)
                if (regs[i] != null)
                    Console.WriteLine( "regs[{0}] = '{1}' = {2}", i, (char)(i + (int)'a'), regs[i].Unparse());
        }

        private void AssignExpression( Node dst, Node expr ) {
            Leaf destination = dst as Leaf;
            regs[destination.Index] = expr;
        }

        private void CallExit() {
            Console.Error.WriteLine( "RealTree will exit" );
            System.Environment.Exit( 1 );
        }

        private double Eval( Node node ) {
            try {
                return node.Eval( this );
            }
            catch (CircularEvalException) {
                Scanner.yyerror( "Eval has circular dependencies" );
            }
            catch {
                Scanner.yyerror( "Invalid expression evaluation" );
            }
            return 0.0;
        }

        private void Display( Node node ) {
            try {
                double result = node.Eval( this );
                Console.WriteLine( "result: " + result.ToString() );
            }
            catch (CircularEvalException) {
                Scanner.yyerror( "Eval has circular dependencies" );
            }
            catch {
                Scanner.yyerror( "Invalid expression evaluation" );
            }
        }

        public class CircularEvalException : Exception {
            internal CircularEvalException() { }
        }
    }

    // ==================================================================================
    //  Start of Node Definitions
    // ==================================================================================

    internal enum NodeTag { error, name, literal, plus, minus, mul, div, rem, negate }

    internal abstract class Node {
        readonly NodeTag tag;
        protected bool active = false;
        public NodeTag Tag { get { return this.tag; } }

        protected Node(NodeTag tag ) { this.tag = tag; }
        public abstract double Eval( Parser p );
        public abstract string Unparse();

        public void Prolog() {
            if (this.active)
                throw new Parser.CircularEvalException();
            this.active = true;
        }

        public void Epilog() { this.active = false; }
    }

    internal class Leaf : Node {
        char name;
        double value;
        internal Leaf( char c ) : base( NodeTag.name ) { this.name = c; }
        internal Leaf(double v ) : base(NodeTag.literal ) { this.value = v; }

        public int Index { get { return (int)name - (int)'a'; } }
        public double Value { get { return value; } }

        public override double Eval( Parser p ) {
            try {
                this.Prolog();
                if (this.Tag == NodeTag.name)
                    return p.regs[this.Index].Eval( p );
                else
                    return this.value;
            }
            finally {
                this.Epilog();
            }
        }

        public override string Unparse() {
            if (Tag == NodeTag.name)
                return this.name.ToString();
            else
                return this.value.ToString();
        }
    }

    internal class Unary : Node {
        Node child;
        internal Unary(NodeTag t, Node c )
            : base( t ) { this.child = c; }

        public override double Eval( Parser p ) {
            try {
                this.Prolog();
                return -this.child.Eval( p );
            }
            finally {
                this.Epilog();
            }
        }

        public override string Unparse() {
            return String.Format( "( - {0})", this.child.Unparse() );
        }
    }

    internal class Binary : Node {
        Node lhs;
        Node rhs;

        internal Binary(NodeTag t, Node l, Node r ) : base( t ) { 
            this.lhs = l; this.rhs = r; 
        }

        public override double Eval( Parser p ) {
            try {
                this.Prolog();
                double lVal = this.lhs.Eval( p );
                double rVal = this.rhs.Eval( p );
                switch (this.Tag) {
                    case NodeTag.div: return lVal / rVal;
                    case NodeTag.minus: return lVal - rVal;
                    case NodeTag.plus: return lVal + rVal;
                    case NodeTag.rem: return lVal % rVal;
                    case NodeTag.mul: return lVal * rVal;
                    default: throw new Exception( "bad tag" );
                }
            }
            finally {
                this.Epilog();
            }
        }

        public override string Unparse() {
            string op = "";
            switch (this.Tag) {
                case NodeTag.div:   op = "/"; break;
                case NodeTag.minus: op = "-"; break;
                case NodeTag.plus:  op = "+"; break;
                case NodeTag.rem:   op = "%"; break;
                case NodeTag.mul:   op = "*"; break;
            }
            return String.Format( "({0} {1} {2})", this.lhs.Unparse(), op, this.rhs.Unparse() );
        }
    }
    // ==================================================================================
}

