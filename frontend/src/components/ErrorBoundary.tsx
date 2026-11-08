import { Component, type ErrorInfo, type ReactNode } from 'react';

interface State {
  failed: boolean;
}

/** Last line of defence: a render crash shows a readable message instead of a blank page. */
export class ErrorBoundary extends Component<{ children: ReactNode }, State> {
  state: State = { failed: false };

  static getDerivedStateFromError(): State {
    return { failed: true };
  }

  componentDidCatch(error: Error, info: ErrorInfo): void {
    console.error('UI crashed:', error, info.componentStack);
  }

  render(): ReactNode {
    if (!this.state.failed) return this.props.children;
    return (
      <div className="fatal" role="alert">
        <h1>Something went wrong</h1>
        <p>
          The page ran into an unexpected problem. Your student ID and your applications are safe.
          Please reload the page. If it keeps happening, contact the school office.
        </p>
        <button className="btn primary" onClick={() => window.location.reload()}>
          Reload the page
        </button>
      </div>
    );
  }
}
