// FsFlow CAPS probe: SRTP member constraint aliases are not valid F#.
// Expected compiler result:
// error FS0010: Unexpected keyword 'when' in interaction. Expected ';', ';;' or other token.

type IEmail =
    abstract Send : string -> unit

type Env =
    { Email : IEmail }

// Invalid: F# does not allow this style of SRTP constraint alias.
type EmailDeps< ^env > = ^env
    when ^env : (member Email : IEmail)
