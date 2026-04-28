import { motion, HTMLMotionProps } from 'framer-motion';
import { ReactNode } from 'react';
import '../styles/components.css';

interface CardProps extends Omit<HTMLMotionProps<'div'>, 'children'> {
  children: ReactNode;
  interactive?: boolean;
  className?: string;
}

export function Card({ children, interactive = false, className = '', ...props }: CardProps) {
  return (
    <motion.div
      className={`card ${interactive ? 'card-interactive' : ''} ${className}`}
      initial={{ opacity: 0, y: 20 }}
      animate={{ opacity: 1, y: 0 }}
      whileHover={interactive ? { y: -4, boxShadow: 'var(--shadow-lg)' } : undefined}
      {...props}
    >
      {children}
    </motion.div>
  );
}

export function CardHeader({ children }: { children: ReactNode }) {
  return <div className="card-header">{children}</div>;
}

export function CardTitle({ children }: { children: ReactNode }) {
  return <h3 className="card-title">{children}</h3>;
}

export function CardSubtitle({ children }: { children: ReactNode }) {
  return <p className="card-subtitle">{children}</p>;
}

export function CardBody({ children }: { children: ReactNode }) {
  return <div className="card-body">{children}</div>;
}

export function CardFooter({ children }: { children: ReactNode }) {
  return <div className="card-footer">{children}</div>;
}
