'use client';

import React from 'react';
import { useSidebar } from '@/components/Sidebar/SidebarProvider';

interface MainContentProps {
  children: React.ReactNode;
}

const MainContent: React.FC<MainContentProps> = ({ children }) => {
  const { isCollapsed } = useSidebar();

  return (
    <main 
      className={`flex-1 transition-all duration-300 ${
        isCollapsed ? 'mr-16' : 'mr-64'
      }`}
    >
      <div className="pr-8">
        {children}
      </div>
    </main>
  );
};

export default MainContent;
