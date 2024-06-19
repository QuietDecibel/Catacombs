#include <QApplication>
#include "startwindow.h"


/*
 This will be the main cpp for the Dungeon Monster Spawner Game.
*/



int main(int argc, char *argv[])
{
    QApplication DMS(argc,argv);
    startwindow Open;
    Open.show();


  return DMS.exec();
}
